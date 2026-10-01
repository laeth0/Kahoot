namespace Kahoot.Application.IntegrationTests.Features.Games.Lobby;

using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Games;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.Features.Games.JoinGame;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Games")]
[Trait("Phase", "08")]
public sealed class GameJoinTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public GameJoinTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Join_NewPlayer_PersistsSeatAndHashedSessionToken()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "LobbyHost1");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Lobby Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        string pin = createResult.Value.Pin;
        Guid gameId = createResult.Value.GameId;

        Guid joinOpId = Guid.NewGuid();
        string ipAddress = "192.168.1.100";
        JoinGameCommand joinCommand = new(pin, "SpeedyFox", joinOpId, ipAddress);

        Result<JoinGameResponse> joinResult = await harness.SendAsync(joinCommand, TestCaller.Anonymous);
        Assert.True(joinResult.IsSuccess);
        JoinGameResponse response = joinResult.Value;

        Assert.NotEqual(Guid.Empty, response.ParticipantId);
        Assert.Equal(gameId, response.GameId);
        Assert.Equal("SpeedyFox", response.Nickname);
        Assert.Equal(1, response.SeatNumber);
        Assert.Equal(68, response.PlayerSessionToken.Length);
        Assert.StartsWith("pst_", response.PlayerSessionToken);

        byte[] expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(response.PlayerSessionToken));

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Participant? participant = await dbContext.Participants
                .FirstOrDefaultAsync(p => p.Id == response.ParticipantId);

            Assert.NotNull(participant);
            Assert.Equal(host.UserId, participant.HostAccountId);
            Assert.Equal(gameId, participant.GameId);
            Assert.Equal("SpeedyFox", participant.DisplayNickname);
            Assert.Equal("SPEEDYFOX", participant.NormalizedNickname);
            Assert.Equal(1, participant.SeatNumber);
            Assert.Equal(1, participant.ConnectionGeneration);
            Assert.False(participant.IsRemoved);
            Assert.Null(participant.RemovedAt);
            Assert.Equal(0, participant.TotalScore);
            Assert.Null(participant.Rank);
            Assert.True(participant.JoinRecoveryExpiresAt > DateTimeOffset.UtcNow);

            List<ParticipantSessionToken> tokens = await dbContext.ParticipantSessionTokens
                .Where(t => t.ParticipantId == response.ParticipantId)
                .ToListAsync();

            Assert.Single(tokens);
            Assert.Equal(expectedHash, tokens[0].TokenHash);
            Assert.Null(tokens[0].RevokedAt);
            Assert.Null(tokens[0].ExpiresAt);

            Game? game = await dbContext.Games.FirstOrDefaultAsync(g => g.Id == gameId);
            Assert.NotNull(game);
            Assert.Equal(1, game.ReservedParticipantCount);
            Assert.Equal(2, game.NextSeatNumber);
            Assert.Equal(1, game.PresenceVersion);
        });

        // Verify Redis rate limit key under harness prefix
        string addressHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ipAddress)));
        string redisRateKey = $"{harness.RedisChannelPrefix}:lobby-join-rate:{addressHash}";
        IDatabase redisDb = _fixture.RedisMultiplexer.GetDatabase();
        bool keyExists = await redisDb.KeyExistsAsync(redisRateKey);
        Assert.True(keyExists);
    }

    [Fact]
    public async Task Join_NormalizedNicknameCollision_IncludingTombstone_Rejects()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "CollisionHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Collision Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        string pin = createResult.Value.Pin;
        Guid gameId = createResult.Value.GameId;

        // Player 1 joins with "SpeedyFox"
        JoinGameCommand join1 = new(pin, "SpeedyFox", Guid.NewGuid(), "192.168.1.1");
        Result<JoinGameResponse> result1 = await harness.SendAsync(join1, TestCaller.Anonymous);
        Assert.True(result1.IsSuccess);

        // Player 2 attempts join with "  speedyfox  " (normalized collision)
        JoinGameCommand join2 = new(pin, "  speedyfox  ", Guid.NewGuid(), "192.168.1.2");
        Result<JoinGameResponse> result2 = await harness.SendAsync(join2, TestCaller.Anonymous);
        Assert.True(result2.IsFailure);
        Assert.Equal(GameErrors.NicknameTaken.Code, result2.Error.Code);

        // Mark Player 1 as removed (tombstone)
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            Participant p1 = await scope.DbContext.Participants.FirstAsync(p => p.Id == result1.Value.ParticipantId);
            p1.IsRemoved = true;
            p1.RemovedAt = DateTimeOffset.UtcNow;
            await scope.DbContext.SaveChangesAsync();
        }

        // Player 3 attempts join with original nickname "SpeedyFox" against tombstone
        JoinGameCommand join3 = new(pin, "SpeedyFox", Guid.NewGuid(), "192.168.1.3");
        Result<JoinGameResponse> result3 = await harness.SendAsync(join3, TestCaller.Anonymous);
        Assert.True(result3.IsFailure);
        Assert.Equal(GameErrors.NicknameTaken.Code, result3.Error.Code);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int participantCount = await dbContext.Participants.CountAsync(p => p.GameId == gameId);
            Assert.Equal(1, participantCount);
        });
    }

    [Fact]
    public async Task Join_AtCapacity_RejectsWithoutChangingCounters()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "CapacityHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Capacity Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        string pin = createResult.Value.Pin;
        Guid gameId = createResult.Value.GameId;

        // Seed 500 valid participants directly into database
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            Game game = await scope.DbContext.Games.FirstAsync(g => g.Id == gameId);
            game.NextSeatNumber = 501;
            game.ReservedParticipantCount = 500;
            game.PresenceVersion = 500;

            List<Participant> participants = new(500);
            for (int seat = 1; seat <= 500; seat++)
            {
                Guid pId = Guid.NewGuid();
                byte[] opHash = SHA256.HashData(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("D").ToLowerInvariant()));
                participants.Add(new Participant
                {
                    Id = pId,
                    HostAccountId = host.UserId,
                    GameId = gameId,
                    DisplayNickname = $"Player_{seat:D3}",
                    NormalizedNickname = $"PLAYER_{seat:D3}",
                    SeatNumber = seat,
                    JoinOperationIdHash = opHash,
                    JoinRecoveryExpiresAt = now.AddMinutes(15),
                    IsRemoved = false,
                    RemovedAt = null,
                    TotalScore = 0,
                    Rank = null,
                    ConnectionGeneration = 1,
                    CreatedAt = now
                });
            }

            scope.DbContext.Participants.AddRange(participants);
            await scope.DbContext.SaveChangesAsync();
        }

        // Attempt 501st join
        JoinGameCommand joinCommand = new(pin, "Player_501", Guid.NewGuid(), "192.168.1.55");
        Result<JoinGameResponse> joinResult = await harness.SendAsync(joinCommand, TestCaller.Anonymous);

        Assert.True(joinResult.IsFailure);
        Assert.Equal(GameErrors.Full.Code, joinResult.Error.Code);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Game game = await dbContext.Games.FirstAsync(g => g.Id == gameId);
            Assert.Equal(500, game.ReservedParticipantCount);
            Assert.Equal(501, game.NextSeatNumber);
            Assert.Equal(500, game.PresenceVersion);

            int totalParticipants = await dbContext.Participants.CountAsync(p => p.GameId == gameId);
            Assert.Equal(500, totalParticipants);
        });
    }

    [Fact]
    public async Task Join_NonLobbyFinishedInvalidOrInactiveHost_Rejects()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "StateGuardHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "State Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        string pin = createResult.Value.Pin;
        Guid gameId = createResult.Value.GameId;

        // Test invalid PIN
        JoinGameCommand invalidPinCommand = new("00000000", "PlayerOne", Guid.NewGuid(), "192.168.1.1");
        Result<JoinGameResponse> invalidPinResult = await harness.SendAsync(invalidPinCommand, TestCaller.Anonymous);
        Assert.True(invalidPinResult.IsFailure);
        Assert.Equal(GameErrors.InvalidPin.Code, invalidPinResult.Error.Code);

        // Mutate game to non-Lobby status (QuestionActive)
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            Game game = await scope.DbContext.Games.FirstAsync(g => g.Id == gameId);
            game.Status = GameStatus.QuestionActive;
            await scope.DbContext.SaveChangesAsync();
        }

        JoinGameCommand nonLobbyCommand = new(pin, "PlayerTwo", Guid.NewGuid(), "192.168.1.2");
        Result<JoinGameResponse> nonLobbyResult = await harness.SendAsync(nonLobbyCommand, TestCaller.Anonymous);
        Assert.True(nonLobbyResult.IsFailure);
        Assert.Equal(GameErrors.NotJoinable.Code, nonLobbyResult.Error.Code);

        // Reset to Lobby, but suspend host
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            Game game = await scope.DbContext.Games.FirstAsync(g => g.Id == gameId);
            game.Status = GameStatus.Lobby;

            User hostUser = await scope.DbContext.Users.FirstAsync(u => u.Id == host.UserId);
            hostUser.Status = UserStatus.Suspended;

            await scope.DbContext.SaveChangesAsync();
        }

        JoinGameCommand suspendedHostCommand = new(pin, "PlayerThree", Guid.NewGuid(), "192.168.1.3");
        Result<JoinGameResponse> suspendedHostResult = await harness.SendAsync(suspendedHostCommand, TestCaller.Anonymous);
        Assert.True(suspendedHostResult.IsFailure);
        Assert.Equal(GameErrors.NotJoinable.Code, suspendedHostResult.Error.Code);
    }

    [Fact]
    public async Task Join_NotificationObservationAndFailureTolerance()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "NotificationHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Notification Quiz");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        string pin = createResult.Value.Pin;
        Guid gameId = createResult.Value.GameId;

        RecordingGameNotificationService notificationService = harness.Services.GetRequiredService<RecordingGameNotificationService>();

        bool callbackObservedCommittedState = false;
        notificationService.Callback = async (GameNotificationRecord record) =>
        {
            Assert.Equal(CancellationToken.None, record.CancellationToken);

            // Read from a separate context to prove participant was committed before notification
            await harness.ReadDbAsync(async (AppDbContext dbContext) =>
            {
                int count = await dbContext.Participants.CountAsync(p => p.GameId == gameId);
                Assert.Equal(1, count);

                Game? game = await dbContext.Games.FirstOrDefaultAsync(g => g.Id == gameId);
                Assert.NotNull(game);
                Assert.Equal(1, game.ReservedParticipantCount);
            });

            callbackObservedCommittedState = true;
        };

        JoinGameCommand join1 = new(pin, "PlayerObserved", Guid.NewGuid(), "192.168.1.1");
        Result<JoinGameResponse> result1 = await harness.SendAsync(join1, TestCaller.Anonymous);
        Assert.True(result1.IsSuccess);
        Assert.True(callbackObservedCommittedState);

        // Now configure callback to throw; join must still succeed (tolerance)
        notificationService.Callback = (GameNotificationRecord _) =>
        {
            throw new InvalidOperationException("Simulated notification service failure.");
        };

        JoinGameCommand join2 = new(pin, "PlayerTolerant", Guid.NewGuid(), "192.168.1.2");
        Result<JoinGameResponse> result2 = await harness.SendAsync(join2, TestCaller.Anonymous);
        Assert.True(result2.IsSuccess);
        Assert.Equal(2, result2.Value.SeatNumber);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            Participant? p2 = await dbContext.Participants.FirstOrDefaultAsync(p => p.Id == result2.Value.ParticipantId);
            Assert.NotNull(p2);
            Assert.Equal("PlayerTolerant", p2.DisplayNickname);
        });
    }
}
