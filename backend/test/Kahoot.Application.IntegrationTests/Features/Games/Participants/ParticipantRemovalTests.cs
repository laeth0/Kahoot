namespace Kahoot.Application.IntegrationTests.Features.Games.Participants;

using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Games;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.Features.Games.EndGame;
using Kahoot.Application.Features.Games.EndQuestion;
using Kahoot.Application.Features.Games.JoinGame;
using Kahoot.Application.Features.Games.Models;
using Kahoot.Application.Features.Games.RemoveParticipant;
using Kahoot.Application.Features.Games.ShowLeaderboard;
using Kahoot.Application.Features.Games.StartGame;
using Kahoot.Application.Features.Games.SubmitAnswer;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Games")]
[Trait("Phase", "11")]
public sealed class ParticipantRemovalTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public ParticipantRemovalTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Remove_InLobby_SetsTombstoneRevokesTokensAndPreservesSeatAllocation()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "RemoveLobbyHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Removal Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand join1 = new(pin, "PlayerOne", Guid.NewGuid(), "192.168.1.1");
        Result<JoinGameResponse> join1Result = await harness.SendAsync(join1, TestCaller.Anonymous);
        Assert.True(join1Result.IsSuccess);
        Guid p1Id = join1Result.Value.ParticipantId;

        JoinGameCommand join2 = new(pin, "PlayerTwo", Guid.NewGuid(), "192.168.1.2");
        Result<JoinGameResponse> join2Result = await harness.SendAsync(join2, TestCaller.Anonymous);
        Assert.True(join2Result.IsSuccess);

        // Remove Player 1
        RemoveParticipantCommand removeCommand = new(gameId, p1Id);
        Result removeResult = await harness.SendAsync(removeCommand, TestCaller.Host(host.UserId));
        Assert.True(removeResult.IsSuccess);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Participant? p1 = await dbContext.Participants.FirstOrDefaultAsync(p => p.Id == p1Id);
            Assert.NotNull(p1);
            Assert.True(p1.IsRemoved);
            Assert.NotNull(p1.RemovedAt);

            List<ParticipantSessionToken> tokens = await dbContext.ParticipantSessionTokens
                .Where(t => t.ParticipantId == p1Id)
                .ToListAsync();

            Assert.Single(tokens);
            Assert.NotNull(tokens[0].RevokedAt);

            Game? game = await dbContext.Games.FirstOrDefaultAsync(g => g.Id == gameId);
            Assert.NotNull(game);
            Assert.Equal(1, game.ReservedParticipantCount);
            Assert.Equal(3, game.NextSeatNumber); // Seat 1 not reused; next seat stays 3
            Assert.Equal(3, game.PresenceVersion); // 0 -> 1 -> 2 -> 3
        });

        // Verify nickname remains reserved against new joins
        JoinGameCommand reJoin = new(pin, "PlayerOne", Guid.NewGuid(), "192.168.1.3");
        Result<JoinGameResponse> reJoinResult = await harness.SendAsync(reJoin, TestCaller.Anonymous);
        Assert.True(reJoinResult.IsFailure);
        Assert.Equal(GameErrors.NicknameTaken.Code, reJoinResult.Error.Code);
    }

    [Fact]
    public async Task Remove_RepeatedRemoval_IsIdempotent()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "IdempotentRemoveHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Idempotent Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand join1 = new(pin, "PlayerIdempotent", Guid.NewGuid(), "192.168.1.1");
        Result<JoinGameResponse> join1Result = await harness.SendAsync(join1, TestCaller.Anonymous);
        Assert.True(join1Result.IsSuccess);
        Guid p1Id = join1Result.Value.ParticipantId;

        RemoveParticipantCommand removeCommand = new(gameId, p1Id);
        Result firstRemove = await harness.SendAsync(removeCommand, TestCaller.Host(host.UserId));
        Assert.True(firstRemove.IsSuccess);

        ControlledPlayerPresenceService presenceService = harness.Services.GetRequiredService<ControlledPlayerPresenceService>();
        int evictionsAfterFirst = presenceService.Evictions.Count;

        // Second removal of already-removed participant
        Result secondRemove = await harness.SendAsync(removeCommand, TestCaller.Host(host.UserId));
        Assert.True(secondRemove.IsSuccess);

        int evictionsAfterSecond = presenceService.Evictions.Count;
        Assert.True(evictionsAfterSecond > evictionsAfterFirst);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game? game = await dbContext.Games.FirstOrDefaultAsync(g => g.Id == gameId);
            Assert.NotNull(game);
            Assert.Equal(0, game.ReservedParticipantCount);
            Assert.Equal(2, game.PresenceVersion); // Not incremented on repeated removal
        });
    }

    [Fact]
    public async Task Remove_DuringQuestionActive_UpdatesEligibilityAndAutoClosesWhenAllRemainingAnswered()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AutoCloseRemoveHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Auto Close Remove Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand join1 = new(pin, "AnsweredPlayer", Guid.NewGuid(), "192.168.1.1");
        Result<JoinGameResponse> join1Result = await harness.SendAsync(join1, TestCaller.Anonymous);
        Assert.True(join1Result.IsSuccess);

        JoinGameCommand join2 = new(pin, "UnansweredPlayer", Guid.NewGuid(), "192.168.1.2");
        Result<JoinGameResponse> join2Result = await harness.SendAsync(join2, TestCaller.Anonymous);
        Assert.True(join2Result.IsSuccess);

        StartGameCommand startCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(startCommand, TestCaller.Host(host.UserId));
        Assert.True(startResult.IsSuccess);
        Guid questionId = startResult.Value.CurrentQuestion.QuestionId;
        Guid choiceId = startResult.Value.CurrentQuestion.Choices[0].ChoiceId;

        // Player 1 answers
        SubmitAnswerCommand submit = new(
            gameId,
            join1Result.Value.ParticipantId,
            questionId,
            new List<Guid> { choiceId },
            ConnectionId: "conn-1",
            SessionTokenHash: null,
            ConnectionGeneration: 1);
        await harness.SendAsync(submit, TestCaller.Anonymous);

        // Player 2 is removed while unanswered.
        // EffectiveEligibleParticipantCount drops from 2 to 1.
        // AcceptedAnswerCount is 1, which now equals EffectiveEligibleParticipantCount (1 > 0).
        // Triggers automatic transition to QuestionResults!
        RemoveParticipantCommand removeCommand = new(gameId, join2Result.Value.ParticipantId);
        Result removeResult = await harness.SendAsync(removeCommand, TestCaller.Host(host.UserId));
        Assert.True(removeResult.IsSuccess);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == gameId);
            Assert.Equal(GameStatus.QuestionResults, game.Status);
            Assert.Equal(3, game.StateVersion);

            GameQuestionSnapshot q = await dbContext.GameQuestionSnapshots.FirstAsync(qs => qs.Id == questionId);
            Assert.Equal(1, q.EffectiveEligibleParticipantCount);
            Assert.Equal(1, q.AcceptedAnswerCount);
        });

        RecordingGameNotificationService notificationService = harness.Services.GetRequiredService<RecordingGameNotificationService>();
        Assert.Contains(notificationService.Records, r => r.EventName == "QuestionEndedWithPersonalResults");
    }

    [Fact]
    public async Task Remove_AnsweredPlayer_PreservesAnswerAndScore()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AnsweredRemoveHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Answered Remove Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand join1 = new(pin, "PlayerWillAnswer", Guid.NewGuid(), "192.168.1.1");
        Result<JoinGameResponse> join1Result = await harness.SendAsync(join1, TestCaller.Anonymous);
        Assert.True(join1Result.IsSuccess);

        JoinGameCommand join2 = new(pin, "PlayerWillRemain", Guid.NewGuid(), "192.168.1.2");
        Result<JoinGameResponse> join2Result = await harness.SendAsync(join2, TestCaller.Anonymous);
        Assert.True(join2Result.IsSuccess);

        StartGameCommand startCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(startCommand, TestCaller.Host(host.UserId));
        Assert.True(startResult.IsSuccess);
        Guid questionId = startResult.Value.CurrentQuestion.QuestionId;
        Guid correctChoiceId = startResult.Value.CurrentQuestion.Choices.First(c => c.IsCorrect == true).ChoiceId;

        // Player 1 answers correctly
        SubmitAnswerCommand submit = new(
            gameId,
            join1Result.Value.ParticipantId,
            questionId,
            new List<Guid> { correctChoiceId },
            ConnectionId: "conn-1",
            SessionTokenHash: null,
            ConnectionGeneration: 1);
        await harness.SendAsync(submit, TestCaller.Anonymous);

        // Host removes Player 1 (the answered player)
        RemoveParticipantCommand removeCommand = new(gameId, join1Result.Value.ParticipantId);
        Result removeResult = await harness.SendAsync(removeCommand, TestCaller.Host(host.UserId));
        Assert.True(removeResult.IsSuccess);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == gameId);
            Assert.Equal(GameStatus.QuestionActive, game.Status); // Still active!
            Assert.Equal(2, game.StateVersion);

            GameQuestionSnapshot q = await dbContext.GameQuestionSnapshots.FirstAsync(qs => qs.Id == questionId);
            Assert.Equal(2, q.EffectiveEligibleParticipantCount); // Not decremented because player answered
            Assert.Equal(1, q.AcceptedAnswerCount);

            AnswerSubmission ans = await dbContext.AnswerSubmissions
                .FirstAsync(a => a.ParticipantId == join1Result.Value.ParticipantId);
            Assert.True(ans.IsCorrect);
            Assert.True(ans.PointsAwarded > 0);
        });
    }

    [Fact]
    public async Task Remove_ToZeroEligibility_DoesNotAutoClose()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "ZeroEligibleHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Zero Eligible Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand join1 = new(pin, "SolePlayer", Guid.NewGuid(), "192.168.1.1");
        Result<JoinGameResponse> join1Result = await harness.SendAsync(join1, TestCaller.Anonymous);
        Assert.True(join1Result.IsSuccess);

        StartGameCommand startCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(startCommand, TestCaller.Host(host.UserId));
        Assert.True(startResult.IsSuccess);
        Guid questionId = startResult.Value.CurrentQuestion.QuestionId;

        // Sole player has not answered. Remove them.
        RemoveParticipantCommand removeCommand = new(gameId, join1Result.Value.ParticipantId);
        Result removeResult = await harness.SendAsync(removeCommand, TestCaller.Host(host.UserId));
        Assert.True(removeResult.IsSuccess);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == gameId);
            Assert.Equal(GameStatus.QuestionActive, game.Status); // Does NOT auto-close to QuestionResults!
            Assert.Equal(2, game.StateVersion);

            GameQuestionSnapshot q = await dbContext.GameQuestionSnapshots.FirstAsync(qs => qs.Id == questionId);
            Assert.Equal(0, q.EffectiveEligibleParticipantCount);
            Assert.Equal(0, q.AcceptedAnswerCount);
        });
    }

    [Fact]
    public async Task Remove_DuringLeaderboard_ReranksSurvivorsAndExcludesRemoved()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "RerankHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Rerank Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand join1 = new(pin, "PlayerGold", Guid.NewGuid(), "192.168.1.1");
        Result<JoinGameResponse> j1 = await harness.SendAsync(join1, TestCaller.Anonymous);

        JoinGameCommand join2 = new(pin, "PlayerSilver", Guid.NewGuid(), "192.168.1.2");
        Result<JoinGameResponse> j2 = await harness.SendAsync(join2, TestCaller.Anonymous);

        JoinGameCommand join3 = new(pin, "PlayerBronze", Guid.NewGuid(), "192.168.1.3");
        Result<JoinGameResponse> j3 = await harness.SendAsync(join3, TestCaller.Anonymous);

        StartGameCommand start = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startRes = await harness.SendAsync(start, TestCaller.Host(host.UserId));
        Guid qId = startRes.Value.CurrentQuestion.QuestionId;
        Guid correctChoiceId = startRes.Value.CurrentQuestion.Choices.First(c => c.IsCorrect == true).ChoiceId;

        // Player 1 answers
        SubmitAnswerCommand ans1 = new(
            gameId, j1.Value.ParticipantId, qId, new List<Guid> { correctChoiceId }, "c1", null, 1);
        await harness.SendAsync(ans1, TestCaller.Anonymous);

        // Player 2 answers
        SubmitAnswerCommand ans2 = new(
            gameId, j2.Value.ParticipantId, qId, new List<Guid> { correctChoiceId }, "c2", null, 1);
        await harness.SendAsync(ans2, TestCaller.Anonymous);

        EndQuestionCommand endQ = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 2);
        await harness.SendAsync(endQ, TestCaller.Host(host.UserId));

        ShowLeaderboardCommand showLb = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 3);
        Result<ShowLeaderboardResponse> lbRes = await harness.SendAsync(showLb, TestCaller.Host(host.UserId));
        Assert.True(lbRes.IsSuccess);

        // In Leaderboard, remove Player 1 (who was rank 1)
        RemoveParticipantCommand removeP1 = new(gameId, j1.Value.ParticipantId);
        Result removeRes = await harness.SendAsync(removeP1, TestCaller.Host(host.UserId));
        Assert.True(removeRes.IsSuccess);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Participant p1 = await dbContext.Participants.FirstAsync(p => p.Id == j1.Value.ParticipantId);
            Assert.True(p1.IsRemoved);
            Assert.Null(p1.Rank);

            Participant p2 = await dbContext.Participants.FirstAsync(p => p.Id == j2.Value.ParticipantId);
            Assert.False(p2.IsRemoved);
            Assert.Equal(1, p2.Rank); // Promoted to Rank 1!

            Participant p3 = await dbContext.Participants.FirstAsync(p => p.Id == j3.Value.ParticipantId);
            Assert.False(p3.IsRemoved);
            Assert.Equal(2, p3.Rank); // Promoted to Rank 2!
        });
    }

    [Fact]
    public async Task Remove_ForeignGameOrFinishedGame_Rejects()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord hostA = await FeatureData.CreateUserAsync(harness, "GuardHostA");
        TestUserRecord hostB = await FeatureData.CreateUserAsync(harness, "GuardHostB");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, hostA.UserId, "Guard Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(hostA.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand join1 = new(pin, "PlayerGuarded", Guid.NewGuid(), "192.168.1.1");
        Result<JoinGameResponse> j1 = await harness.SendAsync(join1, TestCaller.Anonymous);
        Guid participantId = j1.Value.ParticipantId;

        // Foreign host attempts removal -> NotFound
        RemoveParticipantCommand foreignCommand = new(gameId, participantId);
        Result foreignResult = await harness.SendAsync(foreignCommand, TestCaller.Host(hostB.UserId));
        Assert.True(foreignResult.IsFailure);
        Assert.Equal(GameErrors.NotFound.Code, foreignResult.Error.Code);

        // Non-existent participant -> ParticipantNotFound
        RemoveParticipantCommand nonexistentCommand = new(gameId, Guid.NewGuid());
        Result nonexistentResult = await harness.SendAsync(nonexistentCommand, TestCaller.Host(hostA.UserId));
        Assert.True(nonexistentResult.IsFailure);
        Assert.Equal(GameErrors.ParticipantNotFound.Code, nonexistentResult.Error.Code);

        // End Game -> status is Finished
        EndGameCommand endGame = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        await harness.SendAsync(endGame, TestCaller.Host(hostA.UserId));

        // Removal on Finished game -> InvalidStateTransition
        Result finishedResult = await harness.SendAsync(foreignCommand, TestCaller.Host(hostA.UserId));
        Assert.True(finishedResult.IsFailure);
        Assert.Equal(GameErrors.InvalidStateTransition.Code, finishedResult.Error.Code);
    }

    [Fact]
    public async Task Remove_PresenceEvictionFailure_TombstoneAndRevocationRemainCommitted()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "EvictFailHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Evict Fail Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand join1 = new(pin, "PlayerEvictFail", Guid.NewGuid(), "192.168.1.1");
        Result<JoinGameResponse> j1 = await harness.SendAsync(join1, TestCaller.Anonymous);
        Guid participantId = j1.Value.ParticipantId;

        // Configure player presence service eviction callback to throw
        ControlledPlayerPresenceService presenceService = harness.Services.GetRequiredService<ControlledPlayerPresenceService>();
        presenceService.Callback = (EvictedParticipantRecord _) =>
        {
            throw new InvalidOperationException("Simulated socket eviction cluster communication failure.");
        };

        RemoveParticipantCommand removeCommand = new(gameId, participantId);
        Result removeResult = await harness.SendAsync(removeCommand, TestCaller.Host(host.UserId));

        // Handler swallows eviction exception and returns success (removal is already committed)
        Assert.True(removeResult.IsSuccess);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Participant? p = await dbContext.Participants.FirstOrDefaultAsync(pt => pt.Id == participantId);
            Assert.NotNull(p);
            Assert.True(p.IsRemoved);
            Assert.NotNull(p.RemovedAt);

            List<ParticipantSessionToken> tokens = await dbContext.ParticipantSessionTokens
                .Where(t => t.ParticipantId == participantId)
                .ToListAsync();

            Assert.Single(tokens);
            Assert.NotNull(tokens[0].RevokedAt);
        });
    }
}
