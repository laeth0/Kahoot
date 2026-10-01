namespace Kahoot.Application.IntegrationTests.Features.Games.Lobby;

using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Games;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.Features.Games.GetGameParticipants;
using Kahoot.Application.Features.Games.GetJoinInfo;
using Kahoot.Application.Features.Games.JoinGame;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Games")]
[Trait("Phase", "08")]
public sealed class LobbyQueryTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public LobbyQueryTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetJoinInfo_ReturnsCapacityWithoutParticipantSecrets()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "JoinInfoHost");
        TestQuizRecord quiz = await FeatureData.CreatePopulatedQuizAsync(harness, host.UserId, "Space Trivia");

        CreateGameCommand createCommand = new(quiz.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsSuccess);
        string pin = createResult.Value.Pin;
        Guid gameId = createResult.Value.GameId;

        // Join 2 players
        JoinGameCommand join1 = new(pin, "PlayerA", Guid.NewGuid(), "192.168.1.1");
        await harness.SendAsync(join1, TestCaller.Anonymous);

        JoinGameCommand join2 = new(pin, "PlayerB", Guid.NewGuid(), "192.168.1.2");
        await harness.SendAsync(join2, TestCaller.Anonymous);

        GetJoinInfoQuery query = new(pin);
        Result<GetJoinInfoResponse> infoResult = await harness.SendAsync(query, TestCaller.Anonymous);

        Assert.True(infoResult.IsSuccess);
        GetJoinInfoResponse response = infoResult.Value;
        Assert.Equal(gameId, response.GameId);
        Assert.Equal("Space Trivia", response.Title);
        Assert.Equal("LOBBY", response.Status);
        Assert.Equal(2, response.ParticipantCount);
        Assert.Equal(500, response.MaxCapacity);
        Assert.False(response.IsFull);

        // Suspend host -> GetJoinInfo should return InvalidPin
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(host.UserId)))
        {
            User hostUser = await scope.DbContext.Users.FirstAsync(u => u.Id == host.UserId);
            hostUser.Status = UserStatus.Suspended;
            await scope.DbContext.SaveChangesAsync();
        }

        Result<GetJoinInfoResponse> suspendedHostResult = await harness.SendAsync(query, TestCaller.Anonymous);
        Assert.True(suspendedHostResult.IsFailure);
        Assert.Equal(GameErrors.InvalidPin.Code, suspendedHostResult.Error.Code);
    }

    [Fact]
    public async Task GetParticipants_PagesBySeatWithOptionalRemovedRows()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord hostA = await FeatureData.CreateUserAsync(harness, "RosterHostA");
        TestUserRecord hostB = await FeatureData.CreateUserAsync(harness, "RosterHostB");
        TestQuizRecord quizA = await FeatureData.CreatePopulatedQuizAsync(harness, hostA.UserId, "Roster Quiz");

        CreateGameCommand createCommand = new(quizA.QuizId);
        Result<CreateGameResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Host(hostA.UserId));
        Assert.True(createResult.IsSuccess);
        Guid gameId = createResult.Value.GameId;

        // Seed 5 participants: seats 1, 2, 3 (removed), 4, 5
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(hostA.UserId)))
        {
            Game game = await scope.DbContext.Games.FirstAsync(g => g.Id == gameId);
            game.NextSeatNumber = 6;
            game.ReservedParticipantCount = 4; // 4 active
            game.PresenceVersion = 5;

            List<Participant> participants = new()
            {
                new Participant
                {
                    Id = Guid.NewGuid(),
                    HostAccountId = hostA.UserId,
                    GameId = gameId,
                    DisplayNickname = "Seat1_Alpha",
                    NormalizedNickname = "SEAT1_ALPHA",
                    SeatNumber = 1,
                    JoinOperationIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                    JoinRecoveryExpiresAt = now.AddMinutes(15),
                    IsRemoved = false,
                    RemovedAt = null,
                    TotalScore = 0,
                    Rank = null,
                    ConnectionGeneration = 1,
                    CreatedAt = now.AddMinutes(-5)
                },
                new Participant
                {
                    Id = Guid.NewGuid(),
                    HostAccountId = hostA.UserId,
                    GameId = gameId,
                    DisplayNickname = "Seat2_Beta",
                    NormalizedNickname = "SEAT2_BETA",
                    SeatNumber = 2,
                    JoinOperationIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                    JoinRecoveryExpiresAt = now.AddMinutes(15),
                    IsRemoved = false,
                    RemovedAt = null,
                    TotalScore = 0,
                    Rank = null,
                    ConnectionGeneration = 1,
                    CreatedAt = now.AddMinutes(-4)
                },
                new Participant
                {
                    Id = Guid.NewGuid(),
                    HostAccountId = hostA.UserId,
                    GameId = gameId,
                    DisplayNickname = "Seat3_Gamma",
                    NormalizedNickname = "SEAT3_GAMMA",
                    SeatNumber = 3,
                    JoinOperationIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                    JoinRecoveryExpiresAt = now.AddMinutes(15),
                    IsRemoved = true,
                    RemovedAt = now.AddMinutes(-1),
                    TotalScore = 0,
                    Rank = null,
                    ConnectionGeneration = 1,
                    CreatedAt = now.AddMinutes(-3)
                },
                new Participant
                {
                    Id = Guid.NewGuid(),
                    HostAccountId = hostA.UserId,
                    GameId = gameId,
                    DisplayNickname = "Seat4_Delta",
                    NormalizedNickname = "SEAT4_DELTA",
                    SeatNumber = 4,
                    JoinOperationIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                    JoinRecoveryExpiresAt = now.AddMinutes(15),
                    IsRemoved = false,
                    RemovedAt = null,
                    TotalScore = 0,
                    Rank = null,
                    ConnectionGeneration = 1,
                    CreatedAt = now.AddMinutes(-2)
                },
                new Participant
                {
                    Id = Guid.NewGuid(),
                    HostAccountId = hostA.UserId,
                    GameId = gameId,
                    DisplayNickname = "Seat5_Epsilon",
                    NormalizedNickname = "SEAT5_EPSILON",
                    SeatNumber = 5,
                    JoinOperationIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                    JoinRecoveryExpiresAt = now.AddMinutes(15),
                    IsRemoved = false,
                    RemovedAt = null,
                    TotalScore = 0,
                    Rank = null,
                    ConnectionGeneration = 1,
                    CreatedAt = now.AddMinutes(-1)
                }
            };

            scope.DbContext.Participants.AddRange(participants);
            await scope.DbContext.SaveChangesAsync();
        }

        // Page 1: Limit=2, IncludeRemoved=false
        GetGameParticipantsQuery page1Query = new(gameId, IncludeRemoved: false, Limit: 2, Cursor: null);
        Result<GetGameParticipantsResponse> page1Result = await harness.SendAsync(page1Query, TestCaller.Host(hostA.UserId));
        Assert.True(page1Result.IsSuccess);
        Assert.Equal(2, page1Result.Value.Participants.Count);
        Assert.Equal(1, page1Result.Value.Participants[0].SeatNumber);
        Assert.Equal(2, page1Result.Value.Participants[1].SeatNumber);
        Assert.Equal(2, page1Result.Value.NextCursor);

        // Page 2: Limit=2, Cursor=2, IncludeRemoved=false (Seat 3 is removed, so it should be skipped)
        GetGameParticipantsQuery page2Query = new(gameId, IncludeRemoved: false, Limit: 2, Cursor: 2);
        Result<GetGameParticipantsResponse> page2Result = await harness.SendAsync(page2Query, TestCaller.Host(hostA.UserId));
        Assert.True(page2Result.IsSuccess);
        Assert.Equal(2, page2Result.Value.Participants.Count);
        Assert.Equal(4, page2Result.Value.Participants[0].SeatNumber);
        Assert.Equal(5, page2Result.Value.Participants[1].SeatNumber);
        Assert.Null(page2Result.Value.NextCursor);

        // Query with IncludeRemoved=true
        GetGameParticipantsQuery allQuery = new(gameId, IncludeRemoved: true, Limit: 10, Cursor: null);
        Result<GetGameParticipantsResponse> allResult = await harness.SendAsync(allQuery, TestCaller.Host(hostA.UserId));
        Assert.True(allResult.IsSuccess);
        Assert.Equal(5, allResult.Value.Participants.Count);
        Assert.Contains(allResult.Value.Participants, p => p.SeatNumber == 3 && p.IsRemoved);

        // Cross-tenant access: Host B queries Host A's game participants -> NotFound
        Result<GetGameParticipantsResponse> foreignResult = await harness.SendAsync(allQuery, TestCaller.Host(hostB.UserId));
        Assert.True(foreignResult.IsFailure);
        Assert.Equal(GameErrors.NotFound.Code, foreignResult.Error.Code);

        // Anonymous query -> Unauthorized
        Result<GetGameParticipantsResponse> anonResult = await harness.SendAsync(allQuery, TestCaller.Anonymous);
        Assert.True(anonResult.IsFailure);
        Assert.Equal(AuthErrors.Unauthorized.Code, anonResult.Error.Code);
    }
}
