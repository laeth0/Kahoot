namespace Kahoot.Application.IntegrationTests.Features.Games.Creation;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Games;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Games")]
[Trait("Phase", "07")]
public sealed class GamePinCollisionTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public GamePinCollisionTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateGame_ActivePinCollision_RetriesAndSavesWithSecondPin()
    {
        SequencePinGenerator pinGenerator = new(new[] { "123456", "654321" });
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync(services =>
        {
            services.RemoveAll<IPinGeneratorService>();
            services.AddSingleton<IPinGeneratorService>(pinGenerator);
        });

        TestUserRecord hostA = await FeatureData.CreateUserAsync(harness, "HostA");
        TestUserRecord hostB = await FeatureData.CreateUserAsync(harness, "HostB");
        TestQuizRecord quizA = await FeatureData.CreatePopulatedQuizAsync(harness, hostA.UserId, "Quiz A");
        TestQuizRecord quizB = await FeatureData.CreatePopulatedQuizAsync(harness, hostB.UserId, "Quiz B");

        // Seed an active game for Host A with PIN "123456"
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(hostA.UserId)))
        {
            Game existingGame = new()
            {
                Id = Guid.NewGuid(),
                HostAccountId = hostA.UserId,
                SourceQuizId = quizA.QuizId,
                Title = "Existing Active Game",
                Pin = "123456",
                Status = GameStatus.Lobby,
                StateVersion = 1,
                PresenceVersion = 0,
                ReservedParticipantCount = 0,
                NextSeatNumber = 1,
                CurrentQuestionIndex = null,
                HostGraceExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                IsTerminatedBySuspension = false,
                CreatedAt = DateTimeOffset.UtcNow,
                FinishedAt = null
            };
            scope.DbContext.Games.Add(existingGame);
            await scope.DbContext.SaveChangesAsync();
        }

        // Host B creates game; first attempt yields "123456" (collides), second yields "654321"
        CreateGameCommand command = new(quizB.QuizId);
        Result<CreateGameResponse> result = await harness.SendAsync(command, TestCaller.Host(hostB.UserId));

        Assert.True(result.IsSuccess);
        Assert.Equal("654321", result.Value.Pin);
        Assert.Equal(2, pinGenerator.CallCount);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int activeGameCount = await dbContext.Games.CountAsync(g => g.Status == GameStatus.Lobby);
            Assert.Equal(2, activeGameCount);

            Game? gameB = await dbContext.Games.FirstOrDefaultAsync(g => g.Id == result.Value.GameId);
            Assert.NotNull(gameB);
            Assert.Equal("654321", gameB.Pin);
        });
    }

    [Fact]
    public async Task CreateGame_FiveCollisions_ReturnsPinUnavailable()
    {
        SequencePinGenerator pinGenerator = new(new[] { "123456", "123456", "123456", "123456", "123456" });
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync(services =>
        {
            services.RemoveAll<IPinGeneratorService>();
            services.AddSingleton<IPinGeneratorService>(pinGenerator);
        });

        TestUserRecord hostA = await FeatureData.CreateUserAsync(harness, "HostCollidingA");
        TestUserRecord hostB = await FeatureData.CreateUserAsync(harness, "HostCollidingB");
        TestQuizRecord quizA = await FeatureData.CreatePopulatedQuizAsync(harness, hostA.UserId, "Quiz Colliding A");
        TestQuizRecord quizB = await FeatureData.CreatePopulatedQuizAsync(harness, hostB.UserId, "Quiz Colliding B");

        // Seed an active game for Host A with PIN "123456"
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(hostA.UserId)))
        {
            Game existingGame = new()
            {
                Id = Guid.NewGuid(),
                HostAccountId = hostA.UserId,
                SourceQuizId = quizA.QuizId,
                Title = "Active Colliding Target Game",
                Pin = "123456",
                Status = GameStatus.Lobby,
                StateVersion = 1,
                PresenceVersion = 0,
                ReservedParticipantCount = 0,
                NextSeatNumber = 1,
                CurrentQuestionIndex = null,
                HostGraceExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                IsTerminatedBySuspension = false,
                CreatedAt = DateTimeOffset.UtcNow,
                FinishedAt = null
            };
            scope.DbContext.Games.Add(existingGame);
            await scope.DbContext.SaveChangesAsync();
        }

        // Host B creates game; all 5 attempts collide with "123456"
        CreateGameCommand command = new(quizB.QuizId);
        Result<CreateGameResponse> result = await harness.SendAsync(command, TestCaller.Host(hostB.UserId));

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.PinUnavailable.Code, result.Error.Code);
        Assert.Equal(5, pinGenerator.CallCount);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int totalGames = await dbContext.Games.CountAsync();
            Assert.Equal(1, totalGames);

            int hostBGames = await dbContext.Games.CountAsync(g => g.HostAccountId == hostB.UserId);
            Assert.Equal(0, hostBGames);
        });
    }

    [Fact]
    public async Task CreateGame_FinishedGameReleasesPin_AllowsReuse()
    {
        SequencePinGenerator pinGenerator = new(new[] { "999888" });
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync(services =>
        {
            services.RemoveAll<IPinGeneratorService>();
            services.AddSingleton<IPinGeneratorService>(pinGenerator);
        });

        TestUserRecord hostA = await FeatureData.CreateUserAsync(harness, "HostFinishedA");
        TestUserRecord hostB = await FeatureData.CreateUserAsync(harness, "HostNewB");
        TestQuizRecord quizA = await FeatureData.CreatePopulatedQuizAsync(harness, hostA.UserId, "Finished Quiz");
        TestQuizRecord quizB = await FeatureData.CreatePopulatedQuizAsync(harness, hostB.UserId, "New Quiz");

        // Seed a finished game that previously released PIN "999888"
        Guid finishedGameId = Guid.NewGuid();
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(hostA.UserId)))
        {
            Game finishedGame = new()
            {
                Id = finishedGameId,
                HostAccountId = hostA.UserId,
                SourceQuizId = quizA.QuizId,
                Title = "Historical Finished Game",
                Pin = null,
                Status = GameStatus.Finished,
                StateVersion = 5,
                PresenceVersion = 0,
                ReservedParticipantCount = 0,
                NextSeatNumber = 1,
                CurrentQuestionIndex = null,
                HostGraceExpiresAt = null,
                IsTerminatedBySuspension = false,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
                FinishedAt = DateTimeOffset.UtcNow.AddHours(-1)
            };
            scope.DbContext.Games.Add(finishedGame);
            await scope.DbContext.SaveChangesAsync();
        }

        // Host B creates game with the released PIN "999888"
        CreateGameCommand command = new(quizB.QuizId);
        Result<CreateGameResponse> result = await harness.SendAsync(command, TestCaller.Host(hostB.UserId));

        Assert.True(result.IsSuccess);
        Assert.Equal("999888", result.Value.Pin);
        Assert.Equal(1, pinGenerator.CallCount);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game? historicalGame = await dbContext.Games.FirstOrDefaultAsync(g => g.Id == finishedGameId);
            Assert.NotNull(historicalGame);
            Assert.Null(historicalGame.Pin);
            Assert.Equal(GameStatus.Finished, historicalGame.Status);

            Game? activeGame = await dbContext.Games.FirstOrDefaultAsync(g => g.Id == result.Value.GameId);
            Assert.NotNull(activeGame);
            Assert.Equal("999888", activeGame.Pin);
            Assert.Equal(GameStatus.Lobby, activeGame.Status);
        });
    }
}
