namespace Kahoot.Application.IntegrationTests.Features.Games.Gameplay;

using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Games;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.Features.Games.JoinGame;
using Kahoot.Application.Features.Games.StartGame;
using Kahoot.Application.Features.Games.SubmitAnswer;
using Kahoot.Application.IntegrationTests.TestSupport;
using StackExchange.Redis;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Games")]
[Trait("Phase", "10")]
public sealed class AnswerAdmissionTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public AnswerAdmissionTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Submit_TooManyParticipantAttempts_Rejects()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AdmissionHost1");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Admission Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand joinCommand = new(pin, "SpammingPlayer", Guid.NewGuid(), "192.168.1.1");
        Result<JoinGameResponse> joinResult = await harness.SendAsync(joinCommand, TestCaller.Anonymous);
        Assert.True(joinResult.IsSuccess);
        Guid participantId = joinResult.Value.ParticipantId;

        StartGameCommand startCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(startCommand, TestCaller.Host(host.UserId));
        Assert.True(startResult.IsSuccess);
        Guid questionId = startResult.Value.CurrentQuestion.QuestionId;
        Guid validChoiceId = startResult.Value.CurrentQuestion.Choices[0].ChoiceId;

        // Burn 10 attempts using empty connection ID to isolate participant attempt budget
        for (int i = 0; i < 10; i++)
        {
            SubmitAnswerCommand invalidChoiceCommand = new(
                gameId,
                participantId,
                questionId,
                new List<Guid> { Guid.NewGuid() },
                ConnectionId: "",
                SessionTokenHash: null,
                ConnectionGeneration: 1);

            Result<SubmitAnswerResponse> burnResult = await harness.SendAsync(invalidChoiceCommand, TestCaller.Anonymous);
            Assert.True(burnResult.IsFailure);
            Assert.Equal(GameErrors.InvalidChoices.Code, burnResult.Error.Code);
        }

        // 11th attempt with a valid choice must fail with TooManyAnswerAttempts
        SubmitAnswerCommand eleventhCommand = new(
            gameId,
            participantId,
            questionId,
            new List<Guid> { validChoiceId },
            ConnectionId: "",
            SessionTokenHash: null,
            ConnectionGeneration: 1);

        Result<SubmitAnswerResponse> eleventhResult = await harness.SendAsync(eleventhCommand, TestCaller.Anonymous);
        Assert.True(eleventhResult.IsFailure);
        Assert.Equal(GameErrors.TooManyAnswerAttempts.Code, eleventhResult.Error.Code);
    }

    [Fact]
    public async Task Submit_RateKeyDerivation_IgnoresClientQuestionIdBypass()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "AdmissionHost2");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Bypass Prevention Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;
        string pin = createResult.Value.Pin;

        JoinGameCommand joinCommand = new(pin, "BypassPlayer", Guid.NewGuid(), "192.168.1.2");
        Result<JoinGameResponse> joinResult = await harness.SendAsync(joinCommand, TestCaller.Anonymous);
        Assert.True(joinResult.IsSuccess);
        Guid participantId = joinResult.Value.ParticipantId;

        StartGameCommand startCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(startCommand, TestCaller.Host(host.UserId));
        Assert.True(startResult.IsSuccess);
        Guid activeQuestionId = startResult.Value.CurrentQuestion.QuestionId;

        // Send 10 requests each with a distinct client-supplied random QuestionId
        for (int i = 0; i < 10; i++)
        {
            SubmitAnswerCommand randomQuestionCommand = new(
                gameId,
                participantId,
                Guid.NewGuid(), // Client attempts bypass by sending changing question IDs
                new List<Guid> { Guid.NewGuid() },
                ConnectionId: "",
                SessionTokenHash: null,
                ConnectionGeneration: 1);

            await harness.SendAsync(randomQuestionCommand, TestCaller.Anonymous);
        }

        // Verify the Redis counter for the active question reached 10
        string partKey = $"{harness.RedisChannelPrefix}:rate:ans:{{{gameId:N}}}:part:{participantId:N}:{activeQuestionId:N}";
        IDatabase redisDb = _fixture.RedisMultiplexer.GetDatabase();
        RedisValue countValue = await redisDb.StringGetAsync(partKey);
        Assert.True(countValue.HasValue);
        Assert.Equal("10", (string)countValue!);

        // 11th request with the actual active question ID is blocked by rate limiter because the budget was exhausted
        SubmitAnswerCommand eleventhCommand = new(
            gameId,
            participantId,
            activeQuestionId,
            new List<Guid> { Guid.NewGuid() },
            ConnectionId: "",
            SessionTokenHash: null,
            ConnectionGeneration: 1);

        Result<SubmitAnswerResponse> eleventhResult = await harness.SendAsync(eleventhCommand, TestCaller.Anonymous);
        Assert.True(eleventhResult.IsFailure);
        Assert.Equal(GameErrors.TooManyAnswerAttempts.Code, eleventhResult.Error.Code);
    }
}
