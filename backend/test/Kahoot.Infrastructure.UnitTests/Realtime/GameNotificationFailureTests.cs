namespace Kahoot.Infrastructure.UnitTests.Realtime;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Application.Features.Games.Models;
using Kahoot.Infrastructure.Realtime;
using Kahoot.Infrastructure.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Xunit;

public sealed class GameNotificationFailureTests
{
    private readonly RecordingHubContext _hubContext = new();
    private readonly RecordingLogger<GameNotificationService> _logger = new();
    private readonly Guid _hostAccountId = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a301");
    private readonly Guid _gameId = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a302");

    [Fact]
    public async Task PublishAsync_LogsTransportFailureAndContinuesOtherAudience()
    {
        GameNotificationService service = CreateService();
        InvalidOperationException transportException = new("Redis connection reset");

        _hubContext.SendHandler = send =>
        {
            if (send.Group.EndsWith(":players", StringComparison.Ordinal))
            {
                throw transportException;
            }
            return Task.CompletedTask;
        };

        await service.PublishQuestionEndedAsync(_hostAccountId, _gameId, 1, new { QuestionIndex = 0 });

        Assert.Equal(2, _hubContext.Sends.Count);
        Assert.Contains(_hubContext.Sends, s => s.Group.EndsWith(":hosts", StringComparison.Ordinal));

        RecordedLogEntry errorEntry = Assert.Single(_logger.Entries, e => e.LogLevel == LogLevel.Error);
        Assert.Same(transportException, errorEntry.Exception);
        Assert.Equal("QuestionEnded", errorEntry.GetValue("EventName"));
        Assert.Equal(_gameId, errorEntry.GetValue("GameId"));
    }

    [Fact]
    public async Task PublishPersonalEvents_ContinuesAfterOneFailedRecipient()
    {
        GameNotificationService service = CreateService();
        Guid p1 = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a311");
        Guid p2 = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a312");

        PersonalQuestionResultEvent result1 = new(p1, _gameId, 1, Guid.NewGuid(), 0, true, true, 100, 100);
        PersonalQuestionResultEvent result2 = new(p2, _gameId, 1, Guid.NewGuid(), 0, true, false, 0, 50);
        List<PersonalQuestionResultEvent> personalResults = [result1, result2];

        InvalidOperationException recipient1Exception = new("Transport dropped for participant 1");

        _hubContext.SendHandler = send =>
        {
            if (send.Group.Contains(p1.ToString()))
            {
                throw recipient1Exception;
            }
            return Task.CompletedTask;
        };

        await service.PublishQuestionEndedWithPersonalResultsAsync(
            _hostAccountId, _gameId, 1, new { }, personalResults);

        Assert.Equal(4, _hubContext.Sends.Count);
        Assert.Contains(_hubContext.Sends, s => s.Group.Contains(p2.ToString()));

        RecordedLogEntry errorEntry = Assert.Single(_logger.Entries, e => e.LogLevel == LogLevel.Error);
        Assert.Same(recipient1Exception, errorEntry.Exception);
    }

    [Fact]
    public async Task PublishAsync_DoesNotSwallowFailureWhenCallerTokenIsCanceled()
    {
        GameNotificationService service = CreateService();
        using CancellationTokenSource cts = new();
        cts.Cancel();
        CancellationToken canceledToken = cts.Token;

        OperationCanceledException oce = new("Explicitly canceled by caller", canceledToken);

        _hubContext.SendHandler = _ => throw oce;

        OperationCanceledException escapedException = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await service.PublishGameEndedAsync(_hostAccountId, _gameId, 1, new { }, canceledToken);
        });

        Assert.Same(oce, escapedException);
        Assert.DoesNotContain(_logger.Entries, e => e.LogLevel == LogLevel.Error);
    }

    [Fact]
    public async Task PublishAsync_HandlesUnexpectedCancellationAccordingToCatchFilter()
    {
        GameNotificationService service = CreateService();
        using CancellationTokenSource callerCts = new(); // Not cancelled
        CancellationToken callerToken = callerCts.Token;

        OperationCanceledException transportOce = new("Internal transport timeout unrelated to caller");

        _hubContext.SendHandler = _ => throw transportOce;

        // Because callerToken.IsCancellationRequested is false, the catch filter catches and logs it
        await service.PublishGameEndedAsync(_hostAccountId, _gameId, 1, new { }, callerToken);

        Assert.Equal(2, _logger.Entries.Count(e => e.LogLevel == LogLevel.Error));
        RecordedLogEntry firstError = _logger.Entries.First(e => e.LogLevel == LogLevel.Error);
        Assert.Same(transportOce, firstError.Exception);
    }

    private GameNotificationService CreateService()
    {
        return new GameNotificationService(_hubContext, _logger);
    }
}
