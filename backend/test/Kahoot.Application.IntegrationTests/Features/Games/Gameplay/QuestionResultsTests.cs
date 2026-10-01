namespace Kahoot.Application.IntegrationTests.Features.Games.Gameplay;

using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.Features.Games.EndQuestion;
using Kahoot.Application.Features.Games.JoinGame;
using Kahoot.Application.Features.Games.Models;
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
[Trait("Phase", "10")]
public sealed class QuestionResultsTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public QuestionResultsTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Submit_LastEligibleAnswer_MaterializesAndClosesExactlyOnce()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AutoCloseHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Auto Close Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand join1 = new(pin, "PlayerFast", Guid.NewGuid(), "192.168.1.1");
        Result<JoinGameResponse> join1Result = await harness.SendAsync(join1, TestCaller.Anonymous);
        Assert.True(join1Result.IsSuccess);

        JoinGameCommand join2 = new(pin, "PlayerLast", Guid.NewGuid(), "192.168.1.2");
        Result<JoinGameResponse> join2Result = await harness.SendAsync(join2, TestCaller.Anonymous);
        Assert.True(join2Result.IsSuccess);

        StartGameCommand startCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(startCommand, TestCaller.Host(host.UserId));
        Assert.True(startResult.IsSuccess);
        Guid questionId = startResult.Value.CurrentQuestion.QuestionId;
        Guid choiceId = startResult.Value.CurrentQuestion.Choices[0].ChoiceId;

        // Player 1 submits answer
        SubmitAnswerCommand submit1 = new(
            gameId,
            join1Result.Value.ParticipantId,
            questionId,
            new List<Guid> { choiceId },
            ConnectionId: "conn-1",
            SessionTokenHash: null,
            ConnectionGeneration: 1);

        Result<SubmitAnswerResponse> result1 = await harness.SendAsync(submit1, TestCaller.Anonymous);
        Assert.True(result1.IsSuccess);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == gameId);
            Assert.Equal(GameStatus.QuestionActive, game.Status);
            Assert.Equal(2, game.StateVersion);
        });

        // Player 2 submits answer (reaches accepted == effective eligible == 2)
        SubmitAnswerCommand submit2 = new(
            gameId,
            join2Result.Value.ParticipantId,
            questionId,
            new List<Guid> { choiceId },
            ConnectionId: "conn-2",
            SessionTokenHash: null,
            ConnectionGeneration: 1);

        Result<SubmitAnswerResponse> result2 = await harness.SendAsync(submit2, TestCaller.Anonymous);
        Assert.True(result2.IsSuccess);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == gameId);
            Assert.Equal(GameStatus.QuestionResults, game.Status);
            Assert.Equal(3, game.StateVersion);

            GameQuestionSnapshot q = await dbContext.GameQuestionSnapshots.FirstAsync(qs => qs.Id == questionId);
            Assert.NotNull(q.ResultsMaterializedAt);
            Assert.Equal(2, q.AcceptedAnswerCount);
        });

        RecordingGameNotificationService notificationService = harness.Services.GetRequiredService<RecordingGameNotificationService>();
        Assert.Contains(notificationService.Records, r => r.EventName == "QuestionEndedWithPersonalResults");
    }

    [Fact]
    public async Task Submit_AlreadyCommittedAnswer_IsRecoverableReplay()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "ReplayAnsHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(
            harness,
            host.UserId,
            title: "Replay Answer Quiz",
            questionCount: 1,
            choicesPerQuestion: 4);

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand join1 = new(pin, "PlayerReplay", Guid.NewGuid(), "192.168.1.10");
        Result<JoinGameResponse> join1Result = await harness.SendAsync(join1, TestCaller.Anonymous);
        Assert.True(join1Result.IsSuccess);

        JoinGameCommand join2 = new(pin, "PlayerUnanswered", Guid.NewGuid(), "192.168.1.11");
        Result<JoinGameResponse> join2Result = await harness.SendAsync(join2, TestCaller.Anonymous);
        Assert.True(join2Result.IsSuccess);

        StartGameCommand startCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(startCommand, TestCaller.Host(host.UserId));
        Assert.True(startResult.IsSuccess);
        Guid questionId = startResult.Value.CurrentQuestion.QuestionId;
        Guid choiceId = startResult.Value.CurrentQuestion.Choices[0].ChoiceId;

        SubmitAnswerCommand submit = new(
            gameId,
            join1Result.Value.ParticipantId,
            questionId,
            new List<Guid> { choiceId },
            ConnectionId: "conn-1",
            SessionTokenHash: null,
            ConnectionGeneration: 1);

        Result<SubmitAnswerResponse> firstAttempt = await harness.SendAsync(submit, TestCaller.Anonymous);
        Assert.True(firstAttempt.IsSuccess);
        Assert.True(firstAttempt.Value.Accepted);
        Assert.False(firstAttempt.Value.AlreadyAnswered);

        // Immediate duplicate submission while still QuestionActive
        Result<SubmitAnswerResponse> duplicateAttempt = await harness.SendAsync(submit, TestCaller.Anonymous);
        Assert.True(duplicateAttempt.IsSuccess);
        Assert.True(duplicateAttempt.Value.Accepted);
        Assert.True(duplicateAttempt.Value.AlreadyAnswered);

        // Host closes question manually
        EndQuestionCommand endCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 2);
        Result<EndQuestionResponse> endResult = await harness.SendAsync(endCommand, TestCaller.Host(host.UserId));
        Assert.True(endResult.IsSuccess);

        // Resubmit after question ended -> still returns AlreadyAnswered=true
        Result<SubmitAnswerResponse> postCloseAttempt = await harness.SendAsync(submit, TestCaller.Anonymous);
        Assert.True(postCloseAttempt.IsSuccess);
        Assert.True(postCloseAttempt.Value.Accepted);
        Assert.True(postCloseAttempt.Value.AlreadyAnswered);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int totalAnswers = await dbContext.AnswerSubmissions
                .CountAsync(a => a.GameId == gameId && a.ParticipantId == join1Result.Value.ParticipantId);
            Assert.Equal(1, totalAnswers);
        });
    }

    [Fact]
    public async Task EndQuestion_WithPartialAnswers_ProducesNonResponderCards()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "PartialAnsHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Partial Answers Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand join1 = new(pin, "ResponderPlayer", Guid.NewGuid(), "192.168.1.1");
        Result<JoinGameResponse> join1Result = await harness.SendAsync(join1, TestCaller.Anonymous);
        Assert.True(join1Result.IsSuccess);

        JoinGameCommand join2 = new(pin, "NonResponderPlayer", Guid.NewGuid(), "192.168.1.2");
        Result<JoinGameResponse> join2Result = await harness.SendAsync(join2, TestCaller.Anonymous);
        Assert.True(join2Result.IsSuccess);

        StartGameCommand startCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(startCommand, TestCaller.Host(host.UserId));
        Assert.True(startResult.IsSuccess);
        Guid questionId = startResult.Value.CurrentQuestion.QuestionId;
        Guid correctChoiceId = startResult.Value.CurrentQuestion.Choices.First(c => c.IsCorrect == true).ChoiceId;

        // Responder answers correctly
        SubmitAnswerCommand submit = new(
            gameId,
            join1Result.Value.ParticipantId,
            questionId,
            new List<Guid> { correctChoiceId },
            ConnectionId: "conn-1",
            SessionTokenHash: null,
            ConnectionGeneration: 1);

        Result<SubmitAnswerResponse> submitResult = await harness.SendAsync(submit, TestCaller.Anonymous);
        Assert.True(submitResult.IsSuccess);

        // Host manually ends question
        RecordingGameNotificationService notificationService = harness.Services.GetRequiredService<RecordingGameNotificationService>();

        EndQuestionCommand endCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 2);
        Result<EndQuestionResponse> endResult = await harness.SendAsync(endCommand, TestCaller.Host(host.UserId));
        Assert.True(endResult.IsSuccess);

        GameNotificationRecord notificationRecord = Assert.Single(
            notificationService.Records,
            r => r.EventName == "QuestionEndedWithPersonalResults");

        IReadOnlyList<PersonalQuestionResultEvent>? personalCards =
            notificationRecord.SecondaryPayload as IReadOnlyList<PersonalQuestionResultEvent>;
        Assert.NotNull(personalCards);
        Assert.Equal(2, personalCards.Count);

        PersonalQuestionResultEvent responderCard = personalCards.First(p => p.ParticipantId == join1Result.Value.ParticipantId);
        Assert.True(responderCard.Submitted);
        Assert.True(responderCard.IsCorrect);
        Assert.True(responderCard.PointsAwarded > 0);

        PersonalQuestionResultEvent nonResponderCard = personalCards.First(p => p.ParticipantId == join2Result.Value.ParticipantId);
        Assert.False(nonResponderCard.Submitted);
        Assert.False(nonResponderCard.IsCorrect);
        Assert.Equal(0, nonResponderCard.PointsAwarded);
    }
}
