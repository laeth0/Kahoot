namespace Kahoot.Application.IntegrationTests.Features.Games.Lobby;

using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Games;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.Features.Games.JoinGame;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Games")]
[Trait("Phase", "08")]
public sealed class JoinRecoveryTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public JoinRecoveryTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Recover_SameOperationWithoutSocket_ReturnsSameParticipantAndNewToken()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "RecoveryHost1");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Recovery Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        string pin = createResult.Value.Pin;
        Guid gameId = createResult.Value.GameId;

        Guid joinOpId = Guid.NewGuid();
        JoinGameCommand initialJoin = new(pin, "LunaCat", joinOpId, "192.168.1.10");
        Result<JoinGameResponse> initialResult = await harness.SendAsync(initialJoin, TestCaller.Anonymous);
        Assert.True(initialResult.IsSuccess);
        Guid participantId = initialResult.Value.ParticipantId;
        string initialToken = initialResult.Value.PlayerSessionToken;

        // Controlled presence reports no active socket
        ControlledPlayerPresenceService presenceService = harness.Services.GetRequiredService<ControlledPlayerPresenceService>();
        presenceService.SetActive(participantId, false);

        // Replay join with exact same joinOpId, nickname, and pin
        JoinGameCommand recoveryJoin = new(pin, "LunaCat", joinOpId, "192.168.1.10");
        Result<JoinGameResponse> recoveryResult = await harness.SendAsync(recoveryJoin, TestCaller.Anonymous);
        Assert.True(recoveryResult.IsSuccess);
        JoinGameResponse recoveryResponse = recoveryResult.Value;

        Assert.Equal(participantId, recoveryResponse.ParticipantId);
        Assert.Equal(1, recoveryResponse.SeatNumber);
        Assert.Equal("LunaCat", recoveryResponse.Nickname);
        Assert.NotEqual(initialToken, recoveryResponse.PlayerSessionToken);
        Assert.Equal(68, recoveryResponse.PlayerSessionToken.Length);
        Assert.StartsWith("pst_", recoveryResponse.PlayerSessionToken);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == gameId);
            Assert.Equal(1, game.ReservedParticipantCount);
            Assert.Equal(2, game.NextSeatNumber);

            List<ParticipantSessionToken> tokens = await dbContext.ParticipantSessionTokens
                .Where(t => t.ParticipantId == participantId)
                .OrderBy(t => t.CreatedAt)
                .ToListAsync();

            Assert.Equal(2, tokens.Count);
            Assert.Null(tokens[0].RevokedAt);
            Assert.Null(tokens[1].RevokedAt);
        });
    }

    [Fact]
    public async Task Recover_WithActiveSocket_ReturnsEmptyTokenWithoutNewRows()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "RecoveryHost2");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Active Socket Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        string pin = createResult.Value.Pin;
        Guid gameId = createResult.Value.GameId;

        Guid joinOpId = Guid.NewGuid();
        JoinGameCommand initialJoin = new(pin, "StarFox", joinOpId, "192.168.1.20");
        Result<JoinGameResponse> initialResult = await harness.SendAsync(initialJoin, TestCaller.Anonymous);
        Assert.True(initialResult.IsSuccess);
        Guid participantId = initialResult.Value.ParticipantId;

        // Controlled presence reports active socket connection
        ControlledPlayerPresenceService presenceService = harness.Services.GetRequiredService<ControlledPlayerPresenceService>();
        presenceService.SetActive(participantId, true);

        // Replay join
        JoinGameCommand recoveryJoin = new(pin, "StarFox", joinOpId, "192.168.1.20");
        Result<JoinGameResponse> recoveryResult = await harness.SendAsync(recoveryJoin, TestCaller.Anonymous);
        Assert.True(recoveryResult.IsSuccess);

        Assert.Equal(participantId, recoveryResult.Value.ParticipantId);
        Assert.Equal(1, recoveryResult.Value.SeatNumber);
        Assert.Equal(string.Empty, recoveryResult.Value.PlayerSessionToken);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int tokenCount = await dbContext.ParticipantSessionTokens
                .CountAsync(t => t.ParticipantId == participantId);
            Assert.Equal(1, tokenCount);

            Game game = await dbContext.Games.FirstAsync(g => g.Id == gameId);
            Assert.Equal(1, game.ReservedParticipantCount);
        });
    }

    [Fact]
    public async Task Recover_ChangedNicknameOrDifferentGame_IsValidationFailure()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "RecoveryHost3");
        TestQuizRecord quizA = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Quiz A");
        TestQuizRecord quizB = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Quiz B");

        CreateGameCommand createA = new(quizA.QuizId);
        Result<CreateGameResponse> resultA = await harness.SendAsync(createA, TestCaller.Host(host.UserId));
        Assert.True(resultA.IsSuccess);
        string pinA = resultA.Value.Pin;

        CreateGameCommand createB = new(quizB.QuizId);
        Result<CreateGameResponse> resultB = await harness.SendAsync(createB, TestCaller.Host(host.UserId));
        Assert.True(resultB.IsSuccess);
        string pinB = resultB.Value.Pin;

        Guid joinOpId = Guid.NewGuid();
        JoinGameCommand initialJoin = new(pinA, "OriginalName", joinOpId, "192.168.1.30");
        Result<JoinGameResponse> initialResult = await harness.SendAsync(initialJoin, TestCaller.Anonymous);
        Assert.True(initialResult.IsSuccess);

        // Attempt same joinOpId with different nickname
        JoinGameCommand diffNick = new(pinA, "DifferentName", joinOpId, "192.168.1.30");
        Result<JoinGameResponse> diffNickResult = await harness.SendAsync(diffNick, TestCaller.Anonymous);
        Assert.True(diffNickResult.IsFailure);
        Assert.Equal("Validation.Failed", diffNickResult.Error.Code);

        // Attempt same joinOpId with different game PIN
        JoinGameCommand diffGame = new(pinB, "OriginalName", joinOpId, "192.168.1.30");
        Result<JoinGameResponse> diffGameResult = await harness.SendAsync(diffGame, TestCaller.Anonymous);
        Assert.True(diffGameResult.IsFailure);
        Assert.Equal("Validation.Failed", diffGameResult.Error.Code);
    }

    [Fact]
    public async Task Recover_AtFifteenMinutesOrRemoved_DoesNotRestoreSeat()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "RecoveryHost4");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Expiry Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        string pin = createResult.Value.Pin;

        Guid joinOpId = Guid.NewGuid();
        JoinGameCommand initialJoin = new(pin, "ExpiringPlayer", joinOpId, "192.168.1.40");
        Result<JoinGameResponse> initialResult = await harness.SendAsync(initialJoin, TestCaller.Anonymous);
        Assert.True(initialResult.IsSuccess);
        Guid participantId = initialResult.Value.ParticipantId;

        // Set JoinRecoveryExpiresAt to past
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            Participant p = await scope.DbContext.Participants.FirstAsync(pt => pt.Id == participantId);
            p.JoinRecoveryExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-10);
            await scope.DbContext.SaveChangesAsync();
        }

        JoinGameCommand expiredRecovery = new(pin, "ExpiringPlayer", joinOpId, "192.168.1.40");
        Result<JoinGameResponse> expiredResult = await harness.SendAsync(expiredRecovery, TestCaller.Anonymous);
        Assert.True(expiredResult.IsFailure);
        Assert.Equal(GameErrors.NotJoinable.Code, expiredResult.Error.Code);

        // Reset recovery window to future, but mark participant as removed
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            Participant p = await scope.DbContext.Participants.FirstAsync(pt => pt.Id == participantId);
            p.JoinRecoveryExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
            p.IsRemoved = true;
            p.RemovedAt = DateTimeOffset.UtcNow;
            await scope.DbContext.SaveChangesAsync();
        }

        JoinGameCommand removedRecovery = new(pin, "ExpiringPlayer", joinOpId, "192.168.1.40");
        Result<JoinGameResponse> removedResult = await harness.SendAsync(removedRecovery, TestCaller.Anonymous);
        Assert.True(removedResult.IsFailure);
        Assert.Equal(GameErrors.NicknameTaken.Code, removedResult.Error.Code);
    }
}
