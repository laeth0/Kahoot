namespace Kahoot.Infrastructure.UnitTests.Realtime;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Application.Features.Games.Models;
using Kahoot.Infrastructure.Realtime;
using Kahoot.Infrastructure.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class GameNotificationPersonalEventsTests
{
    private readonly RecordingHubContext _hubContext = new();
    private readonly Guid _hostAccountId = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a201");
    private readonly Guid _gameId = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a202");

    [Fact]
    public async Task PublishQuestionEndedWithPersonalResultsAsync_RoutesAggregateThenPersonalEvents()
    {
        GameNotificationService service = CreateService();
        object aggregatePayload = new { QuestionIndex = 0 };

        Guid p1 = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a211");
        Guid p2 = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a212");

        PersonalQuestionResultEvent result1 = new(p1, _gameId, 1, Guid.NewGuid(), 0, true, true, 1000, 1000);
        PersonalQuestionResultEvent result2 = new(p2, _gameId, 1, Guid.NewGuid(), 0, true, false, 0, 500);
        List<PersonalQuestionResultEvent> personalResults = [result1, result2];

        await service.PublishQuestionEndedWithPersonalResultsAsync(_hostAccountId, _gameId, 1, aggregatePayload, personalResults);

        Assert.Equal(4, _hubContext.Sends.Count);

        // Aggregate sends came first
        Assert.Equal($"host:{_hostAccountId}:game:{_gameId}:players", _hubContext.Sends[0].Group);
        Assert.Equal("QuestionEnded", _hubContext.Sends[0].Method);
        Assert.Equal($"host:{_hostAccountId}:game:{_gameId}:hosts", _hubContext.Sends[1].Group);
        Assert.Equal("QuestionEnded", _hubContext.Sends[1].Method);

        // Personal sends targeted individual participant groups
        RecordedSend sendP1 = _hubContext.Sends.Single(s => s.Group == $"host:{_hostAccountId}:game:{_gameId}:participant:{p1}");
        Assert.Equal("PersonalQuestionResult", sendP1.Method);
        Assert.Same(result1, sendP1.SinglePayload);

        RecordedSend sendP2 = _hubContext.Sends.Single(s => s.Group == $"host:{_hostAccountId}:game:{_gameId}:participant:{p2}");
        Assert.Equal("PersonalQuestionResult", sendP2.Method);
        Assert.Same(result2, sendP2.SinglePayload);
    }

    [Fact]
    public async Task PublishQuestionEndedWithPersonalResultsAsync_EmptyPersonalListOnlySendsAggregate()
    {
        GameNotificationService service = CreateService();
        object aggregatePayload = new { QuestionIndex = 0 };

        await service.PublishQuestionEndedWithPersonalResultsAsync(
            _hostAccountId, _gameId, 1, aggregatePayload, Array.Empty<PersonalQuestionResultEvent>());

        Assert.Equal(2, _hubContext.Sends.Count);
        Assert.All(_hubContext.Sends, s => Assert.Equal("QuestionEnded", s.Method));
    }

    [Fact]
    public async Task PublishLeaderboardUpdatedWithPersonalRanksAsync_RoutesAggregateThenPersonalEvents()
    {
        GameNotificationService service = CreateService();
        object aggregatePayload = new { TopPodium = Array.Empty<object>() };

        Guid p1 = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a221");
        PersonalLeaderboardEvent rank1 = new(p1, _gameId, 2, 1, 1500);
        List<PersonalLeaderboardEvent> personalRanks = [rank1];

        await service.PublishLeaderboardUpdatedWithPersonalRanksAsync(_hostAccountId, _gameId, 2, aggregatePayload, personalRanks);

        Assert.Equal(3, _hubContext.Sends.Count);
        RecordedSend personalSend = _hubContext.Sends[2];
        Assert.Equal($"host:{_hostAccountId}:game:{_gameId}:participant:{p1}", personalSend.Group);
        Assert.Equal("PersonalLeaderboardUpdated", personalSend.Method);
        Assert.Same(rank1, personalSend.SinglePayload);
    }

    [Fact]
    public async Task PublishLeaderboardUpdatedWithPersonalRanksAsync_EmptyPersonalListOnlySendsAggregate()
    {
        GameNotificationService service = CreateService();
        object aggregatePayload = new { TopPodium = Array.Empty<object>() };

        await service.PublishLeaderboardUpdatedWithPersonalRanksAsync(
            _hostAccountId, _gameId, 2, aggregatePayload, Array.Empty<PersonalLeaderboardEvent>());

        Assert.Equal(2, _hubContext.Sends.Count);
        Assert.All(_hubContext.Sends, s => Assert.Equal("LeaderboardUpdated", s.Method));
    }

    [Fact]
    public async Task PublishGameEndedWithPersonalRanksAsync_RoutesAggregateThenPersonalEvents()
    {
        GameNotificationService service = CreateService();
        object aggregatePayload = new { FinalRankings = Array.Empty<object>() };

        Guid p1 = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479a231");
        PersonalGameEndedEvent rank1 = new(p1, _gameId, 3, 1, 3000);
        List<PersonalGameEndedEvent> personalRanks = [rank1];

        await service.PublishGameEndedWithPersonalRanksAsync(_hostAccountId, _gameId, 3, aggregatePayload, personalRanks);

        Assert.Equal(3, _hubContext.Sends.Count);
        RecordedSend personalSend = _hubContext.Sends[2];
        Assert.Equal($"host:{_hostAccountId}:game:{_gameId}:participant:{p1}", personalSend.Group);
        Assert.Equal("PersonalGameEnded", personalSend.Method);
        Assert.Same(rank1, personalSend.SinglePayload);
    }

    [Fact]
    public async Task PublishGameEndedWithPersonalRanksAsync_EmptyPersonalListOnlySendsAggregate()
    {
        GameNotificationService service = CreateService();
        object aggregatePayload = new { FinalRankings = Array.Empty<object>() };

        await service.PublishGameEndedWithPersonalRanksAsync(
            _hostAccountId, _gameId, 3, aggregatePayload, Array.Empty<PersonalGameEndedEvent>());

        Assert.Equal(2, _hubContext.Sends.Count);
        Assert.All(_hubContext.Sends, s => Assert.Equal("GameEnded", s.Method));
    }

    [Fact]
    public async Task PublishPersonalEvents_ProcessesAtMostThirtyTwoSendsPerBatch()
    {
        GameNotificationService service = CreateService();
        const int totalParticipants = 33;
        List<PersonalQuestionResultEvent> events = new(totalParticipants);
        List<Guid> participantIds = new(totalParticipants);

        for (int i = 0; i < totalParticipants; i++)
        {
            Guid pId = Guid.NewGuid();
            participantIds.Add(pId);
            events.Add(new PersonalQuestionResultEvent(pId, _gameId, 1, Guid.NewGuid(), 0, true, true, 100, 100));
        }

        ConcurrentDictionary<string, TaskCompletionSource<bool>> pendingSends = new();
        TaskCompletionSource<bool> first32StartedSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> send33StartedSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

        int personalSendsStarted = 0;
        int activeInFlight = 0;
        int maxInFlightObserved = 0;

        _hubContext.SendHandler = send =>
        {
            // Aggregate sends complete immediately
            if (!send.Group.Contains(":participant:"))
            {
                return Task.CompletedTask;
            }

            int started = Interlocked.Increment(ref personalSendsStarted);
            int currentFlight = Interlocked.Increment(ref activeInFlight);

            int currentMax = Volatile.Read(ref maxInFlightObserved);
            while (currentFlight > currentMax)
            {
                int previous = Interlocked.CompareExchange(ref maxInFlightObserved, currentFlight, currentMax);
                if (previous == currentMax)
                {
                    break;
                }
                currentMax = Volatile.Read(ref maxInFlightObserved);
            }

            TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            pendingSends[send.Group] = tcs;

            if (started == 32)
            {
                first32StartedSignal.TrySetResult(true);
            }
            else if (started == 33)
            {
                send33StartedSignal.TrySetResult(true);
            }

            return tcs.Task.ContinueWith(t =>
            {
                Interlocked.Decrement(ref activeInFlight);
            }, TaskScheduler.Default);
        };

        Task publishTask = service.PublishQuestionEndedWithPersonalResultsAsync(
            _hostAccountId, _gameId, 1, new { }, events);

        try
        {
            // 1. Wait until exactly 32 personal sends have started
            await first32StartedSignal.Task;
            Assert.Equal(32, Volatile.Read(ref personalSendsStarted));

            // 2. Release one send from the first batch
            KeyValuePair<string, TaskCompletionSource<bool>> firstSend = pendingSends.First();
            firstSend.Value.TrySetResult(true);

            // Yield briefly to verify 33rd send still has not started
            await Task.Yield();
            Assert.Equal(32, Volatile.Read(ref personalSendsStarted));
            Assert.False(send33StartedSignal.Task.IsCompleted);

            // 3. Release the remaining 31 sends from the first batch
            foreach (KeyValuePair<string, TaskCompletionSource<bool>> item in pendingSends)
            {
                item.Value.TrySetResult(true);
            }

            // 4. Now the 33rd send must start!
            await send33StartedSignal.Task;
            Assert.Equal(33, Volatile.Read(ref personalSendsStarted));

            // 5. Release the 33rd send
            KeyValuePair<string, TaskCompletionSource<bool>> lastSend = pendingSends.Single(s => s.Key.Contains(participantIds[32].ToString()));
            lastSend.Value.TrySetResult(true);

            await publishTask;

            Assert.True(maxInFlightObserved <= 32);
            Assert.Equal(35, _hubContext.Sends.Count); // 2 aggregate + 33 personal
        }
        finally
        {
            // Ensure no pending TCS remain incomplete
            foreach (TaskCompletionSource<bool> tcs in pendingSends.Values)
            {
                tcs.TrySetResult(true);
            }
        }
    }

    private GameNotificationService CreateService()
    {
        return new GameNotificationService(_hubContext, NullLogger<GameNotificationService>.Instance);
    }
}
