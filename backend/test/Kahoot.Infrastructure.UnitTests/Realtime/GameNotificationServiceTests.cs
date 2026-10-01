namespace Kahoot.Infrastructure.UnitTests.Realtime;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Infrastructure.Realtime;
using Kahoot.Infrastructure.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class GameNotificationServiceTests
{
    private readonly RecordingHubContext _hubContext = new();
    private readonly Guid _hostAccountId = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a101");
    private readonly Guid _gameId = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a102");

    [Fact]
    public async Task PublishQuestionStartedAsync_RoutesDistinctAudiencePayloads()
    {
        GameNotificationService service = CreateService();
        object playerPayload = new { QuestionIndex = 1, TimeLimitSeconds = 30 };
        object hostPayload = new { QuestionIndex = 1, CorrectAnswerIndex = 2 };

        await service.PublishQuestionStartedAsync(_hostAccountId, _gameId, 1, playerPayload, hostPayload);

        Assert.Equal(2, _hubContext.Sends.Count);

        string expectedPlayerGroup = $"host:{_hostAccountId}:game:{_gameId}:players";
        string expectedHostGroup = $"host:{_hostAccountId}:game:{_gameId}:hosts";

        RecordedSend playerSend = _hubContext.Sends.Single(s => s.Group == expectedPlayerGroup);
        Assert.Equal("QuestionStarted", playerSend.Method);
        Assert.Same(playerPayload, playerSend.SinglePayload);

        RecordedSend hostSend = _hubContext.Sends.Single(s => s.Group == expectedHostGroup);
        Assert.Equal("QuestionStartedForHost", hostSend.Method);
        Assert.Same(hostPayload, hostSend.SinglePayload);
    }

    [Theory]
    [InlineData("QuestionEnded")]
    [InlineData("LeaderboardUpdated")]
    [InlineData("GameEnded")]
    [InlineData("ParticipantPresenceChanged")]
    public async Task PublishAggregateMethods_RouteToBothHostAndPlayerGroups(string methodType)
    {
        GameNotificationService service = CreateService();
        object payload = new { TestId = Guid.NewGuid(), Status = "Active" };

        switch (methodType)
        {
            case "QuestionEnded":
                await service.PublishQuestionEndedAsync(_hostAccountId, _gameId, 1, payload);
                break;
            case "LeaderboardUpdated":
                await service.PublishLeaderboardUpdatedAsync(_hostAccountId, _gameId, 1, payload);
                break;
            case "GameEnded":
                await service.PublishGameEndedAsync(_hostAccountId, _gameId, 1, payload);
                break;
            case "ParticipantPresenceChanged":
                await service.PublishParticipantPresenceChangedAsync(_hostAccountId, _gameId, 1, payload);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(methodType));
        }

        Assert.Equal(2, _hubContext.Sends.Count);

        string expectedPlayerGroup = $"host:{_hostAccountId}:game:{_gameId}:players";
        string expectedHostGroup = $"host:{_hostAccountId}:game:{_gameId}:hosts";

        RecordedSend playerSend = _hubContext.Sends.Single(s => s.Group == expectedPlayerGroup);
        Assert.Equal(methodType, playerSend.Method);
        Assert.Same(payload, playerSend.SinglePayload);

        RecordedSend hostSend = _hubContext.Sends.Single(s => s.Group == expectedHostGroup);
        Assert.Equal(methodType, hostSend.Method);
        Assert.Same(payload, hostSend.SinglePayload);
    }

    [Fact]
    public async Task PublishAsync_ForwardsCallerTokenWithoutAddingPayloadFields()
    {
        GameNotificationService service = CreateService();
        object payload = new { Message = "ImmutablePayload" };
        using CancellationTokenSource cts = new();
        CancellationToken callerToken = cts.Token;

        await service.PublishGameEndedAsync(_hostAccountId, _gameId, 5, payload, callerToken);

        Assert.Equal(2, _hubContext.Sends.Count);
        foreach (RecordedSend send in _hubContext.Sends)
        {
            Assert.Equal(callerToken, send.CancellationToken);
            Assert.Single(send.Args);
            Assert.Same(payload, send.SinglePayload);
        }
    }

    private GameNotificationService CreateService()
    {
        return new GameNotificationService(_hubContext, NullLogger<GameNotificationService>.Instance);
    }
}
