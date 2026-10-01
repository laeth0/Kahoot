namespace Kahoot.Application.IntegrationTests.Features.Quizzes;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.Features.Quizzes;
using Kahoot.Application.Features.Quizzes.DeleteQuiz;
using Kahoot.Application.Features.Quizzes.UpdateQuiz;
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
[Trait("Feature", "Quizzes")]
[Trait("Phase", "14")]
public sealed class QuizSnapshotConcurrencyTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public QuizSnapshotConcurrencyTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task QuizEditVsCreateGame_SerializesThroughHostRow()
    {
        // Order A: Edit-first snapshot contains the whole committed edit
        {
            await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
            TestUserRecord host = await FeatureData.CreateUserAsync(harness, "QuizRaceHostA", role: UserRole.Host);
            TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Original Title");

            TransactionCommitGate gate1 = new();
            await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Host(host.UserId));
            scope1.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate1;
            int pid1 = await PostgresLockObserver.GetBackendPidAsync(scope1.DbContext);

            await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Host(host.UserId));
            int pid2 = await PostgresLockObserver.GetBackendPidAsync(scope2.DbContext);

            UpdateQuizCommand editCmd = new(quiz.QuizId, "Edited Title", "Edited Description");
            Task<Result<QuizSummaryResponse>> editTask = scope1.Sender.Send(editCmd);
            await gate1.Reached;

            CreateGameCommand createCmd = new(quiz.QuizId);
            Task<Result<CreateGameResponse>> createTask = scope2.Sender.Send(createCmd);
            await PostgresLockObserver.WaitForBlockerAsync(harness.ConnectionString, pid2, pid1, TimeSpan.FromSeconds(5));

            try
            {
                gate1.Release();
                Result<QuizSummaryResponse> editRes = await editTask;
                Assert.True(editRes.IsSuccess);

                Result<CreateGameResponse> createRes = await createTask;
                Assert.True(createRes.IsSuccess);

                // Verify game snapshot captured the committed edit
                await harness.ReadDbAsync(async (AppDbContext dbContext) =>
                {
                    Game game = await dbContext.Games.FirstAsync(g => g.Id == createRes.Value.GameId);
                    Assert.Equal("Edited Title", game.Title);
                });
            }
            finally
            {
                gate1.Release();
            }
        }

        // Order B: Creation-first causes Quiz.InUse for edit
        {
            await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
            TestUserRecord host = await FeatureData.CreateUserAsync(harness, "QuizRaceHostB", role: UserRole.Host);
            TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Original Title");

            TransactionCommitGate gate1 = new();
            await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Host(host.UserId));
            scope1.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate1;
            int pid1 = await PostgresLockObserver.GetBackendPidAsync(scope1.DbContext);

            await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Host(host.UserId));
            int pid2 = await PostgresLockObserver.GetBackendPidAsync(scope2.DbContext);

            CreateGameCommand createCmd = new(quiz.QuizId);
            Task<Result<CreateGameResponse>> createTask = scope1.Sender.Send(createCmd);
            await gate1.Reached;

            UpdateQuizCommand editCmd = new(quiz.QuizId, "Concurrent Title", "Concurrent Description");
            Task<Result<QuizSummaryResponse>> editTask = scope2.Sender.Send(editCmd);
            await PostgresLockObserver.WaitForBlockerAsync(harness.ConnectionString, pid2, pid1, TimeSpan.FromSeconds(5));

            try
            {
                gate1.Release();
                Result<CreateGameResponse> createRes = await createTask;
                Assert.True(createRes.IsSuccess);

                Result<QuizSummaryResponse> editRes = await editTask;
                Assert.True(editRes.IsFailure);
                Assert.Equal(QuizErrors.InUse.Code, editRes.Error.Code);
            }
            finally
            {
                gate1.Release();
            }
        }
    }

    [Fact]
    public async Task QuizDeleteVsCreateGame_RespectsBothCommitOrders()
    {
        // Order A: Delete-first => Quiz.NotFound/no game
        {
            await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
            TestUserRecord host = await FeatureData.CreateUserAsync(harness, "QuizDeleteRaceA", role: UserRole.Host);
            TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Delete Quiz");

            TransactionCommitGate gate1 = new();
            await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Host(host.UserId));
            scope1.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate1;
            int pid1 = await PostgresLockObserver.GetBackendPidAsync(scope1.DbContext);

            await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Host(host.UserId));
            int pid2 = await PostgresLockObserver.GetBackendPidAsync(scope2.DbContext);

            DeleteQuizCommand deleteCmd = new(quiz.QuizId);
            Task<Result> deleteTask = scope1.Sender.Send(deleteCmd);
            await gate1.Reached;

            CreateGameCommand createCmd = new(quiz.QuizId);
            Task<Result<CreateGameResponse>> createTask = scope2.Sender.Send(createCmd);
            await PostgresLockObserver.WaitForBlockerAsync(harness.ConnectionString, pid2, pid1, TimeSpan.FromSeconds(5));

            try
            {
                gate1.Release();
                Result deleteRes = await deleteTask;
                Assert.True(deleteRes.IsSuccess);

                Result<CreateGameResponse> createRes = await createTask;
                Assert.True(createRes.IsFailure);
                Assert.Equal(QuizErrors.NotFound.Code, createRes.Error.Code);
            }
            finally
            {
                gate1.Release();
            }

            await harness.ReadDbAsync(async (AppDbContext dbContext) =>
            {
                int gameCount = await dbContext.Games.CountAsync(g => g.SourceQuizId == quiz.QuizId);
                Assert.Equal(0, gameCount);
            });
        }

        // Order B: Create-first => Quiz.InUse for delete
        {
            await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
            TestUserRecord host = await FeatureData.CreateUserAsync(harness, "QuizDeleteRaceB", role: UserRole.Host);
            TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Delete Quiz B");

            TransactionCommitGate gate1 = new();
            await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Host(host.UserId));
            scope1.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate1;
            int pid1 = await PostgresLockObserver.GetBackendPidAsync(scope1.DbContext);

            await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Host(host.UserId));
            int pid2 = await PostgresLockObserver.GetBackendPidAsync(scope2.DbContext);

            CreateGameCommand createCmd = new(quiz.QuizId);
            Task<Result<CreateGameResponse>> createTask = scope1.Sender.Send(createCmd);
            await gate1.Reached;

            DeleteQuizCommand deleteCmd = new(quiz.QuizId);
            Task<Result> deleteTask = scope2.Sender.Send(deleteCmd);
            await PostgresLockObserver.WaitForBlockerAsync(harness.ConnectionString, pid2, pid1, TimeSpan.FromSeconds(5));

            try
            {
                gate1.Release();
                Result<CreateGameResponse> createRes = await createTask;
                Assert.True(createRes.IsSuccess);

                Result deleteRes = await deleteTask;
                Assert.True(deleteRes.IsFailure);
                Assert.Equal(QuizErrors.InUse.Code, deleteRes.Error.Code);
            }
            finally
            {
                gate1.Release();
            }
        }
    }
}
