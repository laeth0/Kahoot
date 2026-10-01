namespace Kahoot.Application.IntegrationTests.Features.Games.Lifecycle;

using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Games;
using Kahoot.Application.Features.Games.AdvanceQuestion;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.Features.Games.EndGame;
using Kahoot.Application.Features.Games.EndQuestion;
using Kahoot.Application.Features.Games.JoinGame;
using Kahoot.Application.Features.Games.Models;
using Kahoot.Application.Features.Games.ShowLeaderboard;
using Kahoot.Application.Features.Games.StartGame;
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
[Trait("Phase", "09")]
public sealed class GameTransitionTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public GameTransitionTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Transition_TwoQuestionWorkflow_AdvancesThroughCompleteLifecycle()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "WorkflowHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(
            harness,
            host.UserId,
            title: "Two Question Quiz",
            questionCount: 2,
            choicesPerQuestion: 4);

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        // Two players join the lobby
        JoinGameCommand join1 = new(pin, "PlayerAlpha", Guid.NewGuid(), "192.168.1.1");
        Result<JoinGameResponse> join1Result = await harness.SendAsync(join1, TestCaller.Anonymous);
        Assert.True(join1Result.IsSuccess);

        JoinGameCommand join2 = new(pin, "PlayerBeta", Guid.NewGuid(), "192.168.1.2");
        Result<JoinGameResponse> join2Result = await harness.SendAsync(join2, TestCaller.Anonymous);
        Assert.True(join2Result.IsSuccess);

        // --- Step 1: StartGame (LOBBY -> QUESTION_ACTIVE for Question 1) ---
        DateTimeOffset beforeStart = await harness.ReadDbAsync(async (AppDbContext dbContext) =>
            await dbContext.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync());

        StartGameCommand startCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(startCommand, TestCaller.Host(host.UserId));
        Assert.True(startResult.IsSuccess);
        Assert.Equal("QUESTION_ACTIVE", startResult.Value.Status);
        Assert.Equal(2, startResult.Value.StateVersion);
        Assert.Equal(1, startResult.Value.CurrentQuestion.OrderIndex);

        DateTimeOffset afterStart = await harness.ReadDbAsync(async (AppDbContext dbContext) =>
            await dbContext.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync());

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            GameQuestionSnapshot q1 = await dbContext.GameQuestionSnapshots
                .FirstAsync(q => q.GameId == gameId && q.OrderIndex == 1);

            Assert.NotNull(q1.StartedAt);
            Assert.NotNull(q1.EndsAt);
            Assert.True(q1.StartedAt.Value >= beforeStart && q1.StartedAt.Value <= afterStart);
            Assert.Equal(TimeSpan.FromSeconds(30), q1.EndsAt.Value - q1.StartedAt.Value);
            Assert.Equal(2, q1.InitialEligibleParticipantCount);
            Assert.Equal(2, q1.EffectiveEligibleParticipantCount);
        });

        // --- Step 2: EndQuestion (QUESTION_ACTIVE -> QUESTION_RESULTS) ---
        EndQuestionCommand endQ1Command = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 2);
        Result<EndQuestionResponse> endQ1Result = await harness.SendAsync(endQ1Command, TestCaller.Host(host.UserId));
        Assert.True(endQ1Result.IsSuccess);
        Assert.Equal("QUESTION_RESULTS", endQ1Result.Value.Status);
        Assert.Equal(3, endQ1Result.Value.StateVersion);

        // --- Step 3: ShowLeaderboard (QUESTION_RESULTS -> LEADERBOARD) ---
        ShowLeaderboardCommand showLbCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 3);
        Result<ShowLeaderboardResponse> showLbResult = await harness.SendAsync(showLbCommand, TestCaller.Host(host.UserId));
        Assert.True(showLbResult.IsSuccess);
        Assert.Equal("LEADERBOARD", showLbResult.Value.Status);
        Assert.Equal(4, showLbResult.Value.StateVersion);

        // --- Step 4: AdvanceQuestion (LEADERBOARD -> QUESTION_ACTIVE for Question 2) ---
        DateTimeOffset beforeAdvance = await harness.ReadDbAsync(async (AppDbContext dbContext) =>
            await dbContext.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync());

        AdvanceQuestionCommand advanceCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 4);
        Result<AdvanceQuestionResponse> advanceResult = await harness.SendAsync(advanceCommand, TestCaller.Host(host.UserId));
        Assert.True(advanceResult.IsSuccess);
        Assert.Equal("QUESTION_ACTIVE", advanceResult.Value.Status);
        Assert.Equal(5, advanceResult.Value.StateVersion);
        Assert.Equal(2, advanceResult.Value.CurrentQuestion.OrderIndex);

        DateTimeOffset afterAdvance = await harness.ReadDbAsync(async (AppDbContext dbContext) =>
            await dbContext.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync());

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            GameQuestionSnapshot q2 = await dbContext.GameQuestionSnapshots
                .FirstAsync(q => q.GameId == gameId && q.OrderIndex == 2);

            Assert.NotNull(q2.StartedAt);
            Assert.NotNull(q2.EndsAt);
            Assert.True(q2.StartedAt.Value >= beforeAdvance && q2.StartedAt.Value <= afterAdvance);
            Assert.Equal(TimeSpan.FromSeconds(30), q2.EndsAt.Value - q2.StartedAt.Value);
        });

        // --- Step 5: EndQuestion for Question 2 (QUESTION_ACTIVE -> QUESTION_RESULTS) ---
        EndQuestionCommand endQ2Command = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 5);
        Result<EndQuestionResponse> endQ2Result = await harness.SendAsync(endQ2Command, TestCaller.Host(host.UserId));
        Assert.True(endQ2Result.IsSuccess);
        Assert.Equal("QUESTION_RESULTS", endQ2Result.Value.Status);
        Assert.Equal(6, endQ2Result.Value.StateVersion);

        // --- Step 6: EndGame (QUESTION_RESULTS -> FINISHED) ---
        EndGameCommand endGameCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 6);
        Result<EndGameResponse> endGameResult = await harness.SendAsync(endGameCommand, TestCaller.Host(host.UserId));
        Assert.True(endGameResult.IsSuccess);
        Assert.Equal("FINISHED", endGameResult.Value.Status);
        Assert.Equal(7, endGameResult.Value.StateVersion);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game finalGame = await dbContext.Games.FirstAsync(g => g.Id == gameId);
            Assert.Equal(GameStatus.Finished, finalGame.Status);
            Assert.NotNull(finalGame.FinishedAt);
            Assert.Null(finalGame.Pin);
            Assert.Null(finalGame.HostGraceExpiresAt);
            Assert.Equal(7, finalGame.StateVersion);

            List<ParticipantSessionToken> tokens = await dbContext.ParticipantSessionTokens
                .Where(t => t.GameId == gameId)
                .ToListAsync();

            Assert.Equal(2, tokens.Count);
            foreach (ParticipantSessionToken token in tokens)
            {
                Assert.NotNull(token.ExpiresAt);
                Assert.Null(token.RevokedAt);
                Assert.Equal(finalGame.FinishedAt.Value.AddHours(24), token.ExpiresAt.Value);
            }
        });
    }

    [Fact]
    public async Task Transition_QuestionStartedPayloads_AudienceIsolation()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AudienceHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Audience Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;

        RecordingGameNotificationService notificationService = harness.Services.GetRequiredService<RecordingGameNotificationService>();

        bool observedCommittedDuringNotification = false;
        notificationService.Callback = async (GameNotificationRecord record) =>
        {
            if (record.EventName == "QuestionStarted")
            {
                Assert.Equal(CancellationToken.None, record.CancellationToken);

                // Observe DB from fresh context during notification callback
                await harness.ReadDbAsync(async (AppDbContext dbContext) =>
                {
                    Game game = await dbContext.Games.FirstAsync(g => g.Id == gameId);
                    Assert.Equal(GameStatus.QuestionActive, game.Status);
                    Assert.Equal(2, game.StateVersion);
                });

                observedCommittedDuringNotification = true;
            }
        };

        StartGameCommand startCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(startCommand, TestCaller.Host(host.UserId));
        Assert.True(startResult.IsSuccess);
        Assert.True(observedCommittedDuringNotification);

        GameNotificationRecord notificationRecord = Assert.Single(
            notificationService.Records,
            r => r.EventName == "QuestionStarted");

        // Player Payload assertion: choices do NOT contain IsCorrect
        PlayerQuestionStartedEvent playerPayload = Assert.IsType<PlayerQuestionStartedEvent>(notificationRecord.PrimaryPayload);
        Assert.NotNull(playerPayload.Choices);
        Assert.NotEmpty(playerPayload.Choices);
        foreach (PlayerQuestionChoiceDto playerChoice in playerPayload.Choices)
        {
            Assert.NotEqual(Guid.Empty, playerChoice.ChoiceId);
            Assert.True(playerChoice.OrderIndex >= 1);
            Assert.False(string.IsNullOrWhiteSpace(playerChoice.Text));
        }

        // Host Payload assertion: choices contain IsCorrect
        HostQuestionStartedEvent hostPayload = Assert.IsType<HostQuestionStartedEvent>(notificationRecord.SecondaryPayload);
        Assert.NotNull(hostPayload.Choices);
        Assert.NotEmpty(hostPayload.Choices);
        Assert.NotEmpty(hostPayload.CorrectChoiceIds);
        Assert.Contains(hostPayload.Choices, c => c.IsCorrect == true);
    }

    [Fact]
    public async Task Transition_ZeroEligibleParticipants_DoesNotPrematurelyAutoClose()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "ZeroPlayerHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Zero Player Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;

        // Start game with 0 participants in lobby
        StartGameCommand startCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(startCommand, TestCaller.Host(host.UserId));
        Assert.True(startResult.IsSuccess);
        Assert.Equal("QUESTION_ACTIVE", startResult.Value.Status);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == gameId);
            Assert.Equal(GameStatus.QuestionActive, game.Status);
            Assert.Equal(2, game.StateVersion);

            GameQuestionSnapshot q1 = await dbContext.GameQuestionSnapshots
                .FirstAsync(q => q.GameId == gameId && q.OrderIndex == 1);
            Assert.Equal(0, q1.InitialEligibleParticipantCount);
            Assert.Equal(0, q1.EffectiveEligibleParticipantCount);
            Assert.Null(q1.ResultsMaterializedAt);
        });
    }

    [Fact]
    public async Task Transition_LegalAndIllegalStateTransitions()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord hostA = await FeatureData.CreateUserAsync(harness, "IllegalHostA");
        TestUserRecord hostB = await FeatureData.CreateUserAsync(harness, "IllegalHostB");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, hostA.UserId, "State Transitions Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(hostA.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;

        // 1. Illegal transition: EndQuestion while in LOBBY
        EndQuestionCommand prematureEnd = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<EndQuestionResponse> prematureResult = await harness.SendAsync(prematureEnd, TestCaller.Host(hostA.UserId));
        Assert.True(prematureResult.IsFailure);
        Assert.Equal(GameErrors.InvalidStateTransition.Code, prematureResult.Error.Code);

        // 2. Foreign game: Host B calling command on Host A's game -> NotFound
        StartGameCommand foreignCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> foreignResult = await harness.SendAsync(foreignCommand, TestCaller.Host(hostB.UserId));
        Assert.True(foreignResult.IsFailure);
        Assert.Equal(GameErrors.NotFound.Code, foreignResult.Error.Code);

        // 3. Expired host grace period
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(hostA.UserId)))
        {
            Game game = await scope.DbContext.Games.FirstAsync(g => g.Id == gameId);
            game.HostGraceExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5);
            await scope.DbContext.SaveChangesAsync();
        }

        StartGameCommand graceExpiredCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> graceResult = await harness.SendAsync(graceExpiredCommand, TestCaller.Host(hostA.UserId));
        Assert.True(graceResult.IsFailure);
        Assert.Equal(GameErrors.InvalidStateTransition.Code, graceResult.Error.Code);
    }
}
