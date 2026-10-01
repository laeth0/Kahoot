namespace Kahoot.Application.IntegrationTests.Features.Games.Concurrency;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Games;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.Features.Games.JoinGame;
using Kahoot.Application.Features.Games.StartGame;
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
[Trait("Feature", "Games")]
[Trait("Phase", "15")]
public sealed class LobbyConcurrencyTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public LobbyConcurrencyTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConcurrentJoins_ForLastSeat_AdmitOne()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "CapacityHost", role: UserRole.Host);
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Cap Quiz");

        CreateGameResponse createRes = (await harness.SendAsync(new CreateGameCommand(quiz.QuizId), TestCaller.Host(host.UserId))).Value;

        // Seed 499 participants so only 1 seat remains under the 500-player limit
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Anonymous))
        {
            Game game = await scope.DbContext.Games.FirstAsync(g => g.Id == createRes.GameId);
            game.ReservedParticipantCount = 499;
            game.NextSeatNumber = 500;

            for (int index = 1; index <= 499; index++)
            {
                scope.DbContext.Participants.Add(new Participant
                {
                    Id = Guid.NewGuid(),
                    HostAccountId = host.UserId,
                    GameId = game.Id,
                    SeatNumber = index,
                    DisplayNickname = $"Player_{index}",
                    NormalizedNickname = $"PLAYER_{index}",
                    JoinOperationIdHash = SHA256.HashData(Guid.NewGuid().ToByteArray()),
                    JoinRecoveryExpiresAt = now.AddMinutes(2),
                    CreatedAt = now,
                    IsRemoved = false
                });
            }

            await scope.DbContext.SaveChangesAsync();
        }

        TransactionCommitGate gate1 = new();
        await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Anonymous);
        scope1.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate1;
        int pid1 = await PostgresLockObserver.GetBackendPidAsync(scope1.DbContext);

        await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Anonymous);
        int pid2 = await PostgresLockObserver.GetBackendPidAsync(scope2.DbContext);

        JoinGameCommand join1 = new(createRes.Pin, "LastSeatA", Guid.NewGuid(), "10.0.0.1");
        JoinGameCommand join2 = new(createRes.Pin, "LastSeatB", Guid.NewGuid(), "10.0.0.2");

        Task<Result<JoinGameResponse>> task1 = scope1.Sender.Send(join1);
        await gate1.Reached;

        Task<Result<JoinGameResponse>> task2 = scope2.Sender.Send(join2);
        await PostgresLockObserver.WaitForBlockerAsync(harness.ConnectionString, pid2, pid1, TimeSpan.FromSeconds(5));

        try
        {
            gate1.Release();
            Result<JoinGameResponse> result1 = await task1;
            Assert.True(result1.IsSuccess);

            Result<JoinGameResponse> result2 = await task2;
            Assert.True(result2.IsFailure);
            Assert.Equal(GameErrors.Full.Code, result2.Error.Code);
        }
        finally
        {
            gate1.Release();
        }

        // Exact invariant check: 500 seats reserved, next seat 501
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == createRes.GameId);
            Assert.Equal(500, game.ReservedParticipantCount);
            Assert.Equal(501, game.NextSeatNumber);

            int totalParticipants = await dbContext.Participants.CountAsync(p => p.GameId == createRes.GameId);
            Assert.Equal(500, totalParticipants);
        });
    }

    [Fact]
    public async Task ConcurrentJoins_SameNickname_ReserveOneTombstoneName()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "NickRaceHost", role: UserRole.Host);
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Nick Quiz");

        CreateGameResponse createRes = (await harness.SendAsync(new CreateGameCommand(quiz.QuizId), TestCaller.Host(host.UserId))).Value;

        TransactionCommitGate gate1 = new();
        await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Anonymous);
        scope1.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate1;
        int pid1 = await PostgresLockObserver.GetBackendPidAsync(scope1.DbContext);

        await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Anonymous);
        int pid2 = await PostgresLockObserver.GetBackendPidAsync(scope2.DbContext);

        JoinGameCommand join1 = new(createRes.Pin, "SpeedyTiger", Guid.NewGuid(), "10.0.1.1");
        JoinGameCommand join2 = new(createRes.Pin, "speedytiger", Guid.NewGuid(), "10.0.1.2");

        Task<Result<JoinGameResponse>> task1 = scope1.Sender.Send(join1);
        await gate1.Reached;

        Task<Result<JoinGameResponse>> task2 = scope2.Sender.Send(join2);
        await PostgresLockObserver.WaitForBlockerAsync(harness.ConnectionString, pid2, pid1, TimeSpan.FromSeconds(5));

        try
        {
            gate1.Release();
            Result<JoinGameResponse> result1 = await task1;
            Assert.True(result1.IsSuccess);

            Result<JoinGameResponse> result2 = await task2;
            Assert.True(result2.IsFailure);
            Assert.Equal(GameErrors.NicknameTaken.Code, result2.Error.Code);
        }
        finally
        {
            gate1.Release();
        }

        // Exactly one participant committed
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int total = await dbContext.Participants.CountAsync(p => p.GameId == createRes.GameId);
            Assert.Equal(1, total);

            Game game = await dbContext.Games.FirstAsync(g => g.Id == createRes.GameId);
            Assert.Equal(1, game.ReservedParticipantCount);
            Assert.Equal(2, game.NextSeatNumber);
        });
    }

    [Fact]
    public async Task ConcurrentJoinReplay_SameOperation_DoesNotAllocateTwoSeats()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "ReplayHost", role: UserRole.Host);
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Replay Quiz");

        CreateGameResponse createRes = (await harness.SendAsync(new CreateGameCommand(quiz.QuizId), TestCaller.Host(host.UserId))).Value;

        // First join establishes the participant
        Guid replayOpId = Guid.NewGuid();
        JoinGameCommand initialJoin = new(createRes.Pin, "ReplayPlayer", replayOpId, "10.0.2.1");
        Result<JoinGameResponse> initialResult = await harness.SendAsync(initialJoin, TestCaller.Anonymous);
        Assert.True(initialResult.IsSuccess);

        // Now two concurrent requests replay join for the same nickname
        TransactionCommitGate gate1 = new();
        await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Anonymous);
        scope1.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate1;
        int pid1 = await PostgresLockObserver.GetBackendPidAsync(scope1.DbContext);

        await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Anonymous);
        int pid2 = await PostgresLockObserver.GetBackendPidAsync(scope2.DbContext);

        JoinGameCommand replay1 = new(createRes.Pin, "ReplayPlayer", replayOpId, "10.0.2.2");
        JoinGameCommand replay2 = new(createRes.Pin, "ReplayPlayer", replayOpId, "10.0.2.3");

        Task<Result<JoinGameResponse>> task1 = scope1.Sender.Send(replay1);
        await gate1.Reached;

        Task<Result<JoinGameResponse>> task2 = scope2.Sender.Send(replay2);
        await PostgresLockObserver.WaitForBlockerAsync(harness.ConnectionString, pid2, pid1, TimeSpan.FromSeconds(5));

        try
        {
            gate1.Release();
            Result<JoinGameResponse> result1 = await task1;
            Assert.True(result1.IsSuccess);

            Result<JoinGameResponse> result2 = await task2;
            Assert.True(result2.IsSuccess);
        }
        finally
        {
            gate1.Release();
        }

        // Exactly one participant exists, seat counter did not double-increment
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int total = await dbContext.Participants.CountAsync(p => p.GameId == createRes.GameId);
            Assert.Equal(1, total);

            Game game = await dbContext.Games.FirstAsync(g => g.Id == createRes.GameId);
            Assert.Equal(1, game.ReservedParticipantCount);
            Assert.Equal(2, game.NextSeatNumber);
        });
    }

    [Fact]
    public async Task JoinVsStart_RespectsBothCommitOrders()
    {
        // Order A: Join-first participant counted in first-question eligibility
        {
            await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
            TestUserRecord host = await FeatureData.CreateUserAsync(harness, "JoinStartHostA", role: UserRole.Host);
            TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "JS Quiz A");
            CreateGameResponse createRes = (await harness.SendAsync(new CreateGameCommand(quiz.QuizId), TestCaller.Host(host.UserId))).Value;

            TransactionCommitGate gate1 = new();
            await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Anonymous);
            scope1.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate1;
            int pid1 = await PostgresLockObserver.GetBackendPidAsync(scope1.DbContext);

            await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Host(host.UserId));
            int pid2 = await PostgresLockObserver.GetBackendPidAsync(scope2.DbContext);

            JoinGameCommand joinCmd = new(createRes.Pin, "EarlyJoiner", Guid.NewGuid(), "10.0.3.1");
            Task<Result<JoinGameResponse>> joinTask = scope1.Sender.Send(joinCmd);
            await gate1.Reached;

            StartGameCommand startCmd = new(createRes.GameId, Guid.NewGuid(), ExpectedStateVersion: 1);
            Task<Result<StartGameResponse>> startTask = scope2.Sender.Send(startCmd);
            await PostgresLockObserver.WaitForBlockerAsync(harness.ConnectionString, pid2, pid1, TimeSpan.FromSeconds(5));

            try
            {
                gate1.Release();
                Result<JoinGameResponse> joinRes = await joinTask;
                Assert.True(joinRes.IsSuccess);

                Result<StartGameResponse> startRes = await startTask;
                Assert.True(startRes.IsSuccess);
            }
            finally
            {
                gate1.Release();
            }

            // Question snapshot has eligibility = 1
            await harness.ReadDbAsync(async (AppDbContext dbContext) =>
            {
                GameQuestionSnapshot snapshot = await dbContext.GameQuestionSnapshots
                    .FirstAsync(q => q.GameId == createRes.GameId && q.OrderIndex == 1);
                Assert.Equal(1, snapshot.EffectiveEligibleParticipantCount);
            });
        }

        // Order B: Start-first Join => Game.NotJoinable and no new seat
        {
            await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
            TestUserRecord host = await FeatureData.CreateUserAsync(harness, "JoinStartHostB", role: UserRole.Host);
            TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "JS Quiz B");
            CreateGameResponse createRes = (await harness.SendAsync(new CreateGameCommand(quiz.QuizId), TestCaller.Host(host.UserId))).Value;

            // Seed one initial player so game can start
            Result<JoinGameResponse> initialJoin = await harness.SendAsync(
                new JoinGameCommand(createRes.Pin, "ExistingPlayer", Guid.NewGuid(), "10.0.4.1"), TestCaller.Anonymous);
            Assert.True(initialJoin.IsSuccess);

            TransactionCommitGate gate1 = new();
            await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Host(host.UserId));
            scope1.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate1;
            int pid1 = await PostgresLockObserver.GetBackendPidAsync(scope1.DbContext);

            await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Anonymous);
            int pid2 = await PostgresLockObserver.GetBackendPidAsync(scope2.DbContext);

            StartGameCommand startCmd = new(createRes.GameId, Guid.NewGuid(), ExpectedStateVersion: 1);
            Task<Result<StartGameResponse>> startTask = scope1.Sender.Send(startCmd);
            await gate1.Reached;

            JoinGameCommand lateJoin = new(createRes.Pin, "LateJoiner", Guid.NewGuid(), "10.0.4.2");
            Task<Result<JoinGameResponse>> joinTask = scope2.Sender.Send(lateJoin);
            await PostgresLockObserver.WaitForBlockerAsync(harness.ConnectionString, pid2, pid1, TimeSpan.FromSeconds(5));

            try
            {
                gate1.Release();
                Result<StartGameResponse> startRes = await startTask;
                Assert.True(startRes.IsSuccess);

                Result<JoinGameResponse> joinRes = await joinTask;
                Assert.True(joinRes.IsFailure);
                Assert.Equal(GameErrors.NotJoinable.Code, joinRes.Error.Code);
            }
            finally
            {
                gate1.Release();
            }

            // Exactly 1 participant in game, late joiner rejected
            await harness.ReadDbAsync(async (AppDbContext dbContext) =>
            {
                int total = await dbContext.Participants.CountAsync(p => p.GameId == createRes.GameId);
                Assert.Equal(1, total);
            });
        }
    }
}
