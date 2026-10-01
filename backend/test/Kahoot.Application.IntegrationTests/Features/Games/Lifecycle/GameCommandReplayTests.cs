namespace Kahoot.Application.IntegrationTests.Features.Games.Lifecycle;

using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Games;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.Features.Games.EndQuestion;
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
public sealed class GameCommandReplayTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public GameCommandReplayTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Replay_SameCommandIdAndPayload_ReturnsCachedResponseWithoutNewMutations()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "ReplayHost1");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(
            harness,
            host.UserId,
            title: "Replay Quiz",
            questionCount: 2,
            choicesPerQuestion: 4);

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;

        RecordingGameNotificationService notificationService = harness.Services.GetRequiredService<RecordingGameNotificationService>();

        Guid startCommandId = Guid.NewGuid();
        StartGameCommand start1 = new(gameId, startCommandId, ExpectedStateVersion: 1);
        Result<StartGameResponse> result1 = await harness.SendAsync(start1, TestCaller.Host(host.UserId));
        Assert.True(result1.IsSuccess);
        Assert.Equal(2, result1.Value.StateVersion);

        // Immediate replay with same command ID and same payload
        StartGameCommand start2 = new(gameId, startCommandId, ExpectedStateVersion: 1);
        Result<StartGameResponse> result2 = await harness.SendAsync(start2, TestCaller.Host(host.UserId));
        Assert.True(result2.IsSuccess);
        Assert.Equal(result1.Value.StateVersion, result2.Value.StateVersion);
        Assert.Equal(result1.Value.CurrentQuestion.QuestionId, result2.Value.CurrentQuestion.QuestionId);

        // Verify only 1 QuestionStarted event was published
        int startNotifications = notificationService.Records.Count(r => r.EventName == "QuestionStarted");
        Assert.Equal(1, startNotifications);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int idempotencyRecords = await dbContext.GameCommandIdempotencies
                .CountAsync(rec => rec.GameId == gameId && rec.CommandId == startCommandId);
            Assert.Equal(1, idempotencyRecords);

            Game game = await dbContext.Games.FirstAsync(g => g.Id == gameId);
            Assert.Equal(2, game.StateVersion);
        });

        // Advance game to QuestionResults
        EndQuestionCommand endCommand = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 2);
        Result<EndQuestionResponse> endResult = await harness.SendAsync(endCommand, TestCaller.Host(host.UserId));
        Assert.True(endResult.IsSuccess);
        Assert.Equal(3, endResult.Value.StateVersion);

        // Replay original StartGameCommand again; should still return cached response from idempotency cache
        Result<StartGameResponse> result3 = await harness.SendAsync(start2, TestCaller.Host(host.UserId));
        Assert.True(result3.IsSuccess);
        Assert.Equal(2, result3.Value.StateVersion);
    }

    [Fact]
    public async Task Replay_SameCommandIdAlteredPayloadOrCommand_ReturnsValidationFailed()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "ReplayHost2");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Altered Payload Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;

        Guid startCommandId = Guid.NewGuid();
        StartGameCommand initialStart = new(gameId, startCommandId, ExpectedStateVersion: 1);
        Result<StartGameResponse> initialResult = await harness.SendAsync(initialStart, TestCaller.Host(host.UserId));
        Assert.True(initialResult.IsSuccess);

        // Replay with altered ExpectedStateVersion (payload hash mismatch)
        StartGameCommand alteredStart = new(gameId, startCommandId, ExpectedStateVersion: 99);
        Result<StartGameResponse> alteredResult = await harness.SendAsync(alteredStart, TestCaller.Host(host.UserId));
        Assert.True(alteredResult.IsFailure);
        Assert.Equal("Validation.Failed", alteredResult.Error.Code);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == gameId);
            Assert.Equal(2, game.StateVersion);
            Assert.Equal(GameStatus.QuestionActive, game.Status);
        });
    }

    [Fact]
    public async Task StaleExpectedVersion_WithFreshCommandId_ReturnsConcurrentModification()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "ReplayHost3");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Stale Version Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;

        StartGameCommand start = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<StartGameResponse> startResult = await harness.SendAsync(start, TestCaller.Host(host.UserId));
        Assert.True(startResult.IsSuccess);
        Assert.Equal(2, startResult.Value.StateVersion);

        // Send EndQuestion with a fresh command ID but stale expected state version (1 instead of 2)
        EndQuestionCommand staleEnd = new(gameId, Guid.NewGuid(), ExpectedStateVersion: 1);
        Result<EndQuestionResponse> staleResult = await harness.SendAsync(staleEnd, TestCaller.Host(host.UserId));

        Assert.True(staleResult.IsFailure);
        Assert.Equal(GameErrors.ConcurrentModification.Code, staleResult.Error.Code);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == gameId);
            Assert.Equal(2, game.StateVersion);
            Assert.Equal(GameStatus.QuestionActive, game.Status);
        });
    }
}
