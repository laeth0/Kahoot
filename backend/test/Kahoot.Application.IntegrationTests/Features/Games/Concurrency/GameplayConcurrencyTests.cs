namespace Kahoot.Application.IntegrationTests.Features.Games.Concurrency;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Games;
using Kahoot.Application.Features.Games.AdvanceQuestion;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.Features.Games.EndQuestion;
using Kahoot.Application.Features.Games.JoinGame;
using Kahoot.Application.Features.Games.RemoveParticipant;
using Kahoot.Application.Features.Games.StartGame;
using Kahoot.Application.Features.Games.SubmitAnswer;
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
public sealed class GameplayConcurrencyTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public GameplayConcurrencyTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConcurrentAdvance_SameId_ReplaysOneTransition()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AdvanceReplayHost", role: UserRole.Host);
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Adv Quiz 1", questionCount: 2);

        CreateGameResponse createRes = (await harness.SendAsync(new CreateGameCommand(quiz.QuizId), TestCaller.Host(host.UserId))).Value;
        JoinGameResponse joinRes = (await harness.SendAsync(new JoinGameCommand(createRes.Pin, "Player1", Guid.NewGuid(), "10.0.0.1"), TestCaller.Anonymous)).Value;
        StartGameResponse startRes = (await harness.SendAsync(new StartGameCommand(createRes.GameId, Guid.NewGuid(), ExpectedStateVersion: 1), TestCaller.Host(host.UserId))).Value;

        byte[] tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(joinRes.PlayerSessionToken));

        // Auto-close Q1 with an answer submission
        List<Guid> choices = await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            return await dbContext.GameChoiceSnapshots
                .Where(c => c.GameQuestionId == startRes.CurrentQuestion.QuestionId)
                .Select(c => c.Id)
                .ToListAsync();
        });

        SubmitAnswerCommand ansCmd = new(
            createRes.GameId,
            joinRes.ParticipantId,
            startRes.CurrentQuestion.QuestionId,
            new List<Guid> { choices[0] },
            ConnectionId: "c1",
            SessionTokenHash: tokenHash,
            ConnectionGeneration: null);
        await harness.SendAsync(ansCmd, TestCaller.Anonymous);

        // Now in QUESTION_RESULTS, state version is 2
        Guid commandId = Guid.NewGuid();
        TransactionCommitGate gate1 = new();
        await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Host(host.UserId));
        scope1.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate1;
        int pid1 = await PostgresLockObserver.GetBackendPidAsync(scope1.DbContext);

        await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Host(host.UserId));
        int pid2 = await PostgresLockObserver.GetBackendPidAsync(scope2.DbContext);

        AdvanceQuestionCommand advance1 = new(createRes.GameId, CommandId: commandId, ExpectedStateVersion: 3);
        AdvanceQuestionCommand advance2 = new(createRes.GameId, CommandId: commandId, ExpectedStateVersion: 3);

        Task<Result<AdvanceQuestionResponse>> task1 = scope1.Sender.Send(advance1);
        await gate1.Reached;

        Task<Result<AdvanceQuestionResponse>> task2 = scope2.Sender.Send(advance2);
        await PostgresLockObserver.WaitForBlockerAsync(harness.ConnectionString, pid2, pid1, TimeSpan.FromSeconds(5));

        try
        {
            gate1.Release();
            Result<AdvanceQuestionResponse> result1 = await task1;
            Assert.True(result1.IsSuccess);

            Result<AdvanceQuestionResponse> result2 = await task2;
            Assert.True(result2.IsSuccess);

            // Replayed response is identical
            Assert.Equal(result1.Value.StateVersion, result2.Value.StateVersion);
            Assert.Equal(result1.Value.Status, result2.Value.Status);
        }
        finally
        {
            gate1.Release();
        }

        // Database invariant: single state increment and single idempotency record
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == createRes.GameId);
            Assert.Equal(4, game.StateVersion);

            int idempotencyRecords = await dbContext.GameCommandIdempotencies
                .CountAsync(i => i.CommandId == commandId);
            Assert.Equal(1, idempotencyRecords);
        });
    }

    [Fact]
    public async Task ConcurrentAdvance_DifferentIdsSameVersion_RejectsStaleLoser()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AdvanceStaleHost", role: UserRole.Host);
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Adv Quiz 2", questionCount: 2);

        CreateGameResponse createRes = (await harness.SendAsync(new CreateGameCommand(quiz.QuizId), TestCaller.Host(host.UserId))).Value;
        JoinGameResponse joinRes = (await harness.SendAsync(new JoinGameCommand(createRes.Pin, "Player2", Guid.NewGuid(), "10.0.0.2"), TestCaller.Anonymous)).Value;
        StartGameResponse startRes = (await harness.SendAsync(new StartGameCommand(createRes.GameId, Guid.NewGuid(), ExpectedStateVersion: 1), TestCaller.Host(host.UserId))).Value;

        byte[] tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(joinRes.PlayerSessionToken));

        // Auto-close Q1 with an answer submission
        List<Guid> choices = await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            return await dbContext.GameChoiceSnapshots
                .Where(c => c.GameQuestionId == startRes.CurrentQuestion.QuestionId)
                .Select(c => c.Id)
                .ToListAsync();
        });

        SubmitAnswerCommand ansCmd = new(
            createRes.GameId,
            joinRes.ParticipantId,
            startRes.CurrentQuestion.QuestionId,
            new List<Guid> { choices[0] },
            ConnectionId: "c2",
            SessionTokenHash: tokenHash,
            ConnectionGeneration: null);
        await harness.SendAsync(ansCmd, TestCaller.Anonymous);

        // State version is 2
        TransactionCommitGate gate1 = new();
        await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Host(host.UserId));
        scope1.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate1;
        int pid1 = await PostgresLockObserver.GetBackendPidAsync(scope1.DbContext);

        await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Host(host.UserId));
        int pid2 = await PostgresLockObserver.GetBackendPidAsync(scope2.DbContext);

        AdvanceQuestionCommand advance1 = new(createRes.GameId, CommandId: Guid.NewGuid(), ExpectedStateVersion: 3);
        AdvanceQuestionCommand advance2 = new(createRes.GameId, CommandId: Guid.NewGuid(), ExpectedStateVersion: 3);

        Task<Result<AdvanceQuestionResponse>> task1 = scope1.Sender.Send(advance1);
        await gate1.Reached;

        Task<Result<AdvanceQuestionResponse>> task2 = scope2.Sender.Send(advance2);
        await PostgresLockObserver.WaitForBlockerAsync(harness.ConnectionString, pid2, pid1, TimeSpan.FromSeconds(5));

        try
        {
            gate1.Release();
            Result<AdvanceQuestionResponse> result1 = await task1;
            Assert.True(result1.IsSuccess);

            Result<AdvanceQuestionResponse> result2 = await task2;
            Assert.True(result2.IsFailure);
            Assert.Equal(GameErrors.ConcurrentModification.Code, result2.Error.Code);
        }
        finally
        {
            gate1.Release();
        }

        // Database invariant: incremented exactly once to 4
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == createRes.GameId);
            Assert.Equal(4, game.StateVersion);
        });
    }

    [Fact]
    public async Task ConcurrentDuplicateAnswer_ScoresOnce()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AnswerRaceHost", role: UserRole.Host);
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Ans Quiz 1", questionCount: 1);

        CreateGameResponse createRes = (await harness.SendAsync(new CreateGameCommand(quiz.QuizId), TestCaller.Host(host.UserId))).Value;
        JoinGameResponse joinRes = (await harness.SendAsync(new JoinGameCommand(createRes.Pin, "SoloPlayer", Guid.NewGuid(), "10.0.0.3"), TestCaller.Anonymous)).Value;
        StartGameResponse startRes = (await harness.SendAsync(new StartGameCommand(createRes.GameId, Guid.NewGuid(), ExpectedStateVersion: 1), TestCaller.Host(host.UserId))).Value;

        byte[] tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(joinRes.PlayerSessionToken));

        List<Guid> correctChoices = await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            return await dbContext.GameChoiceSnapshots
                .Where(c => c.GameQuestionId == startRes.CurrentQuestion.QuestionId && c.IsCorrect)
                .Select(c => c.Id)
                .ToListAsync();
        });

        // Two concurrent duplicate answer submissions with distinct connection IDs
        SubmitAnswerCommand ans1 = new(
            createRes.GameId,
            joinRes.ParticipantId,
            startRes.CurrentQuestion.QuestionId,
            correctChoices,
            ConnectionId: "conn-dup-1",
            SessionTokenHash: tokenHash,
            ConnectionGeneration: null);

        SubmitAnswerCommand ans2 = new(
            createRes.GameId,
            joinRes.ParticipantId,
            startRes.CurrentQuestion.QuestionId,
            correctChoices,
            ConnectionId: "conn-dup-2",
            SessionTokenHash: tokenHash,
            ConnectionGeneration: null);

        Task<Result<SubmitAnswerResponse>> task1 = harness.SendAsync(ans1, TestCaller.Anonymous);
        Task<Result<SubmitAnswerResponse>> task2 = harness.SendAsync(ans2, TestCaller.Anonymous);

        Result<SubmitAnswerResponse>[] results = await Task.WhenAll(task1, task2);
        Assert.All(results, r => Assert.True(r.IsSuccess));

        // One recorded as new answer, one as already answered
        Assert.Contains(results, r => r.Value.Accepted && !r.Value.AlreadyAnswered);
        Assert.Contains(results, r => r.Value.Accepted && r.Value.AlreadyAnswered);

        // Database invariant: exactly one submission row, one accepted count
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int subCount = await dbContext.AnswerSubmissions
                .CountAsync(a => a.GameId == createRes.GameId && a.ParticipantId == joinRes.ParticipantId);
            Assert.Equal(1, subCount);

            GameQuestionSnapshot snapshot = await dbContext.GameQuestionSnapshots
                .FirstAsync(q => q.Id == startRes.CurrentQuestion.QuestionId);
            Assert.Equal(1, snapshot.AcceptedAnswerCount);

            Participant participant = await dbContext.Participants.FirstAsync(p => p.Id == joinRes.ParticipantId);
            Assert.True(participant.TotalScore > 0);
        });
    }

    [Fact]
    public async Task ConcurrentDistinctAnswers_PreserveEveryScoreAndCount()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "MultiAnsHost", role: UserRole.Host);
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "MultiAns Quiz", questionCount: 1);

        CreateGameResponse createRes = (await harness.SendAsync(new CreateGameCommand(quiz.QuizId), TestCaller.Host(host.UserId))).Value;

        // Join 8 participants
        List<JoinGameResponse> participants = new();
        for (int i = 1; i <= 8; i++)
        {
            JoinGameResponse join = (await harness.SendAsync(
                new JoinGameCommand(createRes.Pin, $"Player_{i}", Guid.NewGuid(), $"10.0.10.{i}"), TestCaller.Anonymous)).Value;
            participants.Add(join);
        }

        StartGameResponse startRes = (await harness.SendAsync(new StartGameCommand(createRes.GameId, Guid.NewGuid(), ExpectedStateVersion: 1), TestCaller.Host(host.UserId))).Value;

        List<Guid> correctChoices = await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            return await dbContext.GameChoiceSnapshots
                .Where(c => c.GameQuestionId == startRes.CurrentQuestion.QuestionId && c.IsCorrect)
                .Select(c => c.Id)
                .ToListAsync();
        });

        // 8 distinct answer submissions fired simultaneously
        List<Task<Result<SubmitAnswerResponse>>> tasks = new();
        for (int i = 0; i < 8; i++)
        {
            JoinGameResponse p = participants[i];
            byte[] pTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(p.PlayerSessionToken));
            SubmitAnswerCommand cmd = new(
                createRes.GameId,
                p.ParticipantId,
                startRes.CurrentQuestion.QuestionId,
                correctChoices,
                ConnectionId: $"conn-sub-{i}",
                SessionTokenHash: pTokenHash,
                ConnectionGeneration: null);

            tasks.Add(Task.Run(async () =>
            {
                await using ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Anonymous);
                return await scope.Sender.Send(cmd);
            }));
        }

        Result<SubmitAnswerResponse>[] results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.True(r.IsSuccess));

        // Exact invariant checks
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int subCount = await dbContext.AnswerSubmissions
                .CountAsync(a => a.GameId == createRes.GameId && a.GameQuestionId == startRes.CurrentQuestion.QuestionId);
            Assert.Equal(8, subCount);

            GameQuestionSnapshot snapshot = await dbContext.GameQuestionSnapshots
                .FirstAsync(q => q.Id == startRes.CurrentQuestion.QuestionId);
            Assert.Equal(8, snapshot.AcceptedAnswerCount);

            List<Participant> players = await dbContext.Participants
                .Where(p => p.GameId == createRes.GameId)
                .ToListAsync();
            Assert.All(players, p => Assert.True(p.TotalScore > 0));

            // All 8 answered, so game automatically transitioned to QuestionResults
            Game game = await dbContext.Games.FirstAsync(g => g.Id == createRes.GameId);
            Assert.Equal(GameStatus.QuestionResults, game.Status);
        });
    }

    [Fact]
    public async Task AnswerVsEndQuestion_RespectsBothCommitOrders()
    {
        // Order A: Answer-first is included in results
        {
            await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
            TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AnsEndHostA", role: UserRole.Host);
            TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "AnsEnd Quiz A", questionCount: 1);

            CreateGameResponse createRes = (await harness.SendAsync(new CreateGameCommand(quiz.QuizId), TestCaller.Host(host.UserId))).Value;
            JoinGameResponse p1 = (await harness.SendAsync(new JoinGameCommand(createRes.Pin, "P1A", Guid.NewGuid(), "10.0.1.1"), TestCaller.Anonymous)).Value;
            JoinGameResponse p2 = (await harness.SendAsync(new JoinGameCommand(createRes.Pin, "P2A", Guid.NewGuid(), "10.0.1.2"), TestCaller.Anonymous)).Value;
            StartGameResponse startRes = (await harness.SendAsync(new StartGameCommand(createRes.GameId, Guid.NewGuid(), ExpectedStateVersion: 1), TestCaller.Host(host.UserId))).Value;

            byte[] p1TokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(p1.PlayerSessionToken));

            List<Guid> correctChoices = await harness.ReadDbAsync(async (AppDbContext dbContext) =>
            {
                return await dbContext.GameChoiceSnapshots
                    .Where(c => c.GameQuestionId == startRes.CurrentQuestion.QuestionId && c.IsCorrect)
                    .Select(c => c.Id)
                    .ToListAsync();
            });

            // Answer first, then host ends question
            SubmitAnswerCommand ansCmd = new(
                createRes.GameId,
                p1.ParticipantId,
                startRes.CurrentQuestion.QuestionId,
                correctChoices,
                ConnectionId: "cp1a",
                SessionTokenHash: p1TokenHash,
                ConnectionGeneration: null);

            Result<SubmitAnswerResponse> ansRes = await harness.SendAsync(ansCmd, TestCaller.Anonymous);
            Assert.True(ansRes.IsSuccess);

            EndQuestionCommand endCmd = new(createRes.GameId, Guid.NewGuid(), ExpectedStateVersion: 2);
            Result<EndQuestionResponse> endRes = await harness.SendAsync(endCmd, TestCaller.Host(host.UserId));
            Assert.True(endRes.IsSuccess);

            await harness.ReadDbAsync(async (AppDbContext dbContext) =>
            {
                int subCount = await dbContext.AnswerSubmissions
                    .CountAsync(a => a.GameId == createRes.GameId && a.ParticipantId == p1.ParticipantId);
                Assert.Equal(1, subCount);
            });
        }

        // Order B: Close-first returns Game.InvalidStateTransition
        {
            await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
            TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AnsEndHostB", role: UserRole.Host);
            TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "AnsEnd Quiz B", questionCount: 1);

            CreateGameResponse createRes = (await harness.SendAsync(new CreateGameCommand(quiz.QuizId), TestCaller.Host(host.UserId))).Value;
            JoinGameResponse p1 = (await harness.SendAsync(new JoinGameCommand(createRes.Pin, "P1B", Guid.NewGuid(), "10.0.2.1"), TestCaller.Anonymous)).Value;
            StartGameResponse startRes = (await harness.SendAsync(new StartGameCommand(createRes.GameId, Guid.NewGuid(), ExpectedStateVersion: 1), TestCaller.Host(host.UserId))).Value;

            byte[] p1TokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(p1.PlayerSessionToken));

            List<Guid> correctChoices = await harness.ReadDbAsync(async (AppDbContext dbContext) =>
            {
                return await dbContext.GameChoiceSnapshots
                    .Where(c => c.GameQuestionId == startRes.CurrentQuestion.QuestionId && c.IsCorrect)
                    .Select(c => c.Id)
                    .ToListAsync();
            });

            // Host ends question first
            EndQuestionCommand endCmd = new(createRes.GameId, Guid.NewGuid(), ExpectedStateVersion: 2);
            Result<EndQuestionResponse> endRes = await harness.SendAsync(endCmd, TestCaller.Host(host.UserId));
            Assert.True(endRes.IsSuccess);

            // Answer after close is rejected
            SubmitAnswerCommand ansCmd = new(
                createRes.GameId,
                p1.ParticipantId,
                startRes.CurrentQuestion.QuestionId,
                correctChoices,
                ConnectionId: "cp1b",
                SessionTokenHash: p1TokenHash,
                ConnectionGeneration: null);

            Result<SubmitAnswerResponse> ansRes = await harness.SendAsync(ansCmd, TestCaller.Anonymous);
            Assert.True(ansRes.IsFailure);
            Assert.Equal(GameErrors.InvalidStateTransition.Code, ansRes.Error.Code);
        }
    }

    [Fact]
    public async Task AnswerVsRemoval_RespectsBothCommitOrders()
    {
        // Order A: Answer-first remains in history after removal
        {
            await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
            TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AnsRemHostA", role: UserRole.Host);
            TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "AnsRem Quiz A", questionCount: 1);

            CreateGameResponse createRes = (await harness.SendAsync(new CreateGameCommand(quiz.QuizId), TestCaller.Host(host.UserId))).Value;
            JoinGameResponse p1 = (await harness.SendAsync(new JoinGameCommand(createRes.Pin, "P1A", Guid.NewGuid(), "10.0.3.1"), TestCaller.Anonymous)).Value;
            JoinGameResponse p2 = (await harness.SendAsync(new JoinGameCommand(createRes.Pin, "P2A", Guid.NewGuid(), "10.0.3.2"), TestCaller.Anonymous)).Value;
            StartGameResponse startRes = (await harness.SendAsync(new StartGameCommand(createRes.GameId, Guid.NewGuid(), ExpectedStateVersion: 1), TestCaller.Host(host.UserId))).Value;

            byte[] p1TokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(p1.PlayerSessionToken));

            List<Guid> correctChoices = await harness.ReadDbAsync(async (AppDbContext dbContext) =>
            {
                return await dbContext.GameChoiceSnapshots
                    .Where(c => c.GameQuestionId == startRes.CurrentQuestion.QuestionId && c.IsCorrect)
                    .Select(c => c.Id)
                    .ToListAsync();
            });

            SubmitAnswerCommand ansCmd = new(
                createRes.GameId,
                p1.ParticipantId,
                startRes.CurrentQuestion.QuestionId,
                correctChoices,
                ConnectionId: "cr1a",
                SessionTokenHash: p1TokenHash,
                ConnectionGeneration: null);
            Result<SubmitAnswerResponse> ansRes = await harness.SendAsync(ansCmd, TestCaller.Anonymous);
            Assert.True(ansRes.IsSuccess);

            RemoveParticipantCommand removeCmd = new(createRes.GameId, p1.ParticipantId);
            Result removeRes = await harness.SendAsync(removeCmd, TestCaller.Host(host.UserId));
            Assert.True(removeRes.IsSuccess);

            await harness.ReadDbAsync(async (AppDbContext dbContext) =>
            {
                int subCount = await dbContext.AnswerSubmissions
                    .CountAsync(a => a.GameId == createRes.GameId && a.ParticipantId == p1.ParticipantId);
                Assert.Equal(1, subCount);

                Participant participant = await dbContext.Participants.FirstAsync(p => p.Id == p1.ParticipantId);
                Assert.True(participant.IsRemoved);
            });
        }

        // Order B: Removal-first => Game.ParticipantRemoved
        {
            await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
            TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AnsRemHostB", role: UserRole.Host);
            TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "AnsRem Quiz B", questionCount: 1);

            CreateGameResponse createRes = (await harness.SendAsync(new CreateGameCommand(quiz.QuizId), TestCaller.Host(host.UserId))).Value;
            JoinGameResponse p1 = (await harness.SendAsync(new JoinGameCommand(createRes.Pin, "P1B", Guid.NewGuid(), "10.0.4.1"), TestCaller.Anonymous)).Value;
            StartGameResponse startRes = (await harness.SendAsync(new StartGameCommand(createRes.GameId, Guid.NewGuid(), ExpectedStateVersion: 1), TestCaller.Host(host.UserId))).Value;

            byte[] p1TokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(p1.PlayerSessionToken));

            List<Guid> correctChoices = await harness.ReadDbAsync(async (AppDbContext dbContext) =>
            {
                return await dbContext.GameChoiceSnapshots
                    .Where(c => c.GameQuestionId == startRes.CurrentQuestion.QuestionId && c.IsCorrect)
                    .Select(c => c.Id)
                    .ToListAsync();
            });

            // Remove participant first
            RemoveParticipantCommand removeCmd = new(createRes.GameId, p1.ParticipantId);
            Result removeRes = await harness.SendAsync(removeCmd, TestCaller.Host(host.UserId));
            Assert.True(removeRes.IsSuccess);

            // Attempted answer after removal is rejected
            SubmitAnswerCommand ansCmd = new(
                createRes.GameId,
                p1.ParticipantId,
                startRes.CurrentQuestion.QuestionId,
                correctChoices,
                ConnectionId: "cr1b",
                SessionTokenHash: p1TokenHash,
                ConnectionGeneration: null);
            Result<SubmitAnswerResponse> ansRes = await harness.SendAsync(ansCmd, TestCaller.Anonymous);
            Assert.True(ansRes.IsFailure);
            Assert.Equal(GameErrors.ParticipantRemoved.Code, ansRes.Error.Code);
        }
    }

    [Fact]
    public async Task LastAnswerVsEligibilityReduction_ClosesOnce()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "LastAnsHost", role: UserRole.Host);
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "LastAns Quiz", questionCount: 1);

        CreateGameResponse createRes = (await harness.SendAsync(new CreateGameCommand(quiz.QuizId), TestCaller.Host(host.UserId))).Value;
        JoinGameResponse p1 = (await harness.SendAsync(new JoinGameCommand(createRes.Pin, "PlayerA", Guid.NewGuid(), "10.0.5.1"), TestCaller.Anonymous)).Value;
        JoinGameResponse p2 = (await harness.SendAsync(new JoinGameCommand(createRes.Pin, "PlayerB", Guid.NewGuid(), "10.0.5.2"), TestCaller.Anonymous)).Value;
        StartGameResponse startRes = (await harness.SendAsync(new StartGameCommand(createRes.GameId, Guid.NewGuid(), ExpectedStateVersion: 1), TestCaller.Host(host.UserId))).Value;

        byte[] p1TokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(p1.PlayerSessionToken));

        List<Guid> correctChoices = await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            return await dbContext.GameChoiceSnapshots
                .Where(c => c.GameQuestionId == startRes.CurrentQuestion.QuestionId && c.IsCorrect)
                .Select(c => c.Id)
                .ToListAsync();
        });

        // Player A submits answer
        SubmitAnswerCommand ansA = new(
            createRes.GameId,
            p1.ParticipantId,
            startRes.CurrentQuestion.QuestionId,
            correctChoices,
            ConnectionId: "cpa",
            SessionTokenHash: p1TokenHash,
            ConnectionGeneration: null);
        Result<SubmitAnswerResponse> ansResA = await harness.SendAsync(ansA, TestCaller.Anonymous);
        Assert.True(ansResA.IsSuccess);

        // Player B is removed, which reduces effective eligible participants from 2 to 1
        // Since accepted is 1, accepted == effective, so removal auto-closes question to QuestionResults
        RemoveParticipantCommand removeB = new(createRes.GameId, p2.ParticipantId);
        Result remRes = await harness.SendAsync(removeB, TestCaller.Host(host.UserId));
        Assert.True(remRes.IsSuccess);

        // Verify question closed once and version incremented once
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == createRes.GameId);
            Assert.Equal(GameStatus.QuestionResults, game.Status);
            Assert.Equal(3, game.StateVersion);

            GameQuestionSnapshot snapshot = await dbContext.GameQuestionSnapshots
                .FirstAsync(q => q.Id == startRes.CurrentQuestion.QuestionId);
            Assert.Equal(1, snapshot.AcceptedAnswerCount);
            Assert.Equal(1, snapshot.EffectiveEligibleParticipantCount);
        });
    }
}
