namespace Kahoot.Application.IntegrationTests.Features.Reliability;

using System;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.Features.Quizzes.CreateQuiz;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Application.IntegrationTests.TestSupport.Concurrency;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Reliability")]
[Trait("Phase", "16")]
public sealed class CancellationTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public CancellationTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Request_CancelledWhileWaitingForHeldHostLock_LeavesNoChanges()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "CancelLockHost", role: UserRole.Host);
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Cancel Lock Quiz");

        TransactionCommitGate gate1 = new();
        await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Host(host.UserId));
        scope1.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate1;
        int pid1 = await PostgresLockObserver.GetBackendPidAsync(scope1.DbContext);

        // Scope 1 acquires host lock and holds it at the commit gate
        CreateGameCommand cmd1 = new(quiz.QuizId);
        Task<Result<CreateGameResponse>> task1 = scope1.Sender.Send(cmd1);
        await gate1.Reached;

        // Scope 2 attempts to create another game for the same host, which requires the host lock
        CancellationTokenSource cts2 = new();
        await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Host(host.UserId));
        int pid2 = await PostgresLockObserver.GetBackendPidAsync(scope2.DbContext);

        CreateGameCommand cmd2 = new(quiz.QuizId);
        Task<Result<CreateGameResponse>> task2 = scope2.Sender.Send(cmd2, cts2.Token);

        // Wait until Scope 2 is observed blocked in PostgreSQL by Scope 1
        await PostgresLockObserver.WaitForBlockerAsync(harness.ConnectionString, pid2, pid1, TimeSpan.FromSeconds(5));

        // Cancel Scope 2 while it is waiting on the lock
        cts2.Cancel();

        // Scope 2 aborts due to cancellation
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await task2;
        });

        // Release Scope 1 and let it complete normally
        gate1.Release();
        Result<CreateGameResponse> result1 = await task1;
        Assert.True(result1.IsSuccess);

        // Scope 3: Later request succeeds after blocker is released
        await using ApplicationRequestScope scope3 = harness.CreateRequestScope(TestCaller.Host(host.UserId));
        CreateGameCommand cmd3 = new(quiz.QuizId);
        Result<CreateGameResponse> result3 = await scope3.Sender.Send(cmd3);
        Assert.True(result3.IsSuccess);

        // Invariant: Exactly 2 games were created (cmd1 and cmd3), cmd2 left zero database trace
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int totalGames = await dbContext.Games.CountAsync(g => g.HostAccountId == host.UserId);
            Assert.Equal(2, totalGames);
        });
    }

    [Fact]
    public async Task Request_CancelledBeforeCommit_RollsBackGraph()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "CancelCommitHost", role: UserRole.Host);

        TransactionCommitGate gate = new();
        CancellationTokenSource cts = new();

        CreateQuizCommand createQuizCmd = new("Cancelled Quiz Title", "Description that should roll back");

        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            scope.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate;

            Task<Result<Kahoot.Application.Features.Quizzes.QuizSummaryResponse>> task = scope.Sender.Send(createQuizCmd, cts.Token);
            await gate.Reached;

            // Cancel the caller's cancellation token
            cts.Cancel();

            // Release the gate
            gate.Release();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await task;
            });
        }

        // Database invariant: Quiz was rolled back and never persisted
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            bool quizExists = await dbContext.Quizzes.AnyAsync(q => q.Title == "Cancelled Quiz Title");
            Assert.False(quizExists);
        });
    }
}
