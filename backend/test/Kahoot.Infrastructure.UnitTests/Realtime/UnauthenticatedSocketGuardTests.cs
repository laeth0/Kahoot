namespace Kahoot.Infrastructure.UnitTests.Realtime;

using System;
using System.Threading;
using Kahoot.Infrastructure.Realtime;
using Kahoot.Infrastructure.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class UnauthenticatedSocketGuardTests
{
    private readonly RecordingTimerTimeProvider _timeProvider = new();

    [Fact]
    public void Track_SchedulesOneShotFifteenSecondTimeout()
    {
        UnauthenticatedSocketGuard guard = CreateGuard();
        const string connectionId = "conn-alpha-1";
        int abortCount = 0;

        guard.Track(connectionId, () => abortCount++);

        Assert.Single(_timeProvider.Timers);
        RecordingTimerTimeProvider.RecordedTimer timer = _timeProvider.Timers[0];
        Assert.Equal(TimeSpan.FromSeconds(15), timer.DueTime);
        Assert.Equal(Timeout.InfiniteTimeSpan, timer.Period);
        Assert.Equal(connectionId, timer.State);
        Assert.False(timer.IsDisposed);
        Assert.Equal(0, abortCount);
    }

    [Fact]
    public void Timeout_AbortsTrackedConnectionAndDisposesTimerOnce()
    {
        UnauthenticatedSocketGuard guard = CreateGuard();
        const string connectionId = "conn-bravo-2";
        int abortCount = 0;

        guard.Track(connectionId, () => abortCount++);
        RecordingTimerTimeProvider.RecordedTimer timer = _timeProvider.Timers[0];

        timer.FireCallback();

        Assert.Equal(1, abortCount);
        Assert.True(timer.IsDisposed);
        Assert.Equal(1, timer.DisposeCount);

        // Firing the queued callback again must not abort again
        timer.FireCallback();
        Assert.Equal(1, abortCount);
    }

    [Fact]
    public void MarkAuthenticated_DisposesTimerAndSuppressesQueuedTimeout()
    {
        UnauthenticatedSocketGuard guard = CreateGuard();
        const string connectionId = "conn-charlie-3";
        int abortCount = 0;

        guard.Track(connectionId, () => abortCount++);
        RecordingTimerTimeProvider.RecordedTimer timer = _timeProvider.Timers[0];

        guard.MarkAuthenticated(connectionId);

        Assert.True(timer.IsDisposed);
        Assert.Equal(1, timer.DisposeCount);

        // Fire a callback that was supposedly already queued before disposal
        timer.FireCallback();
        Assert.Equal(0, abortCount);
    }

    [Fact]
    public void Remove_DisposesTimerAndSuppressesQueuedTimeout()
    {
        UnauthenticatedSocketGuard guard = CreateGuard();
        const string connectionId = "conn-delta-4";
        int abortCount = 0;

        guard.Track(connectionId, () => abortCount++);
        RecordingTimerTimeProvider.RecordedTimer timer = _timeProvider.Timers[0];

        guard.Remove(connectionId);

        Assert.True(timer.IsDisposed);
        Assert.Equal(1, timer.DisposeCount);

        // Fire previously queued callback
        timer.FireCallback();
        Assert.Equal(0, abortCount);
    }

    [Fact]
    public void Track_DuplicateIdDisposesNewTimerAndRetainsOriginalRegistration()
    {
        UnauthenticatedSocketGuard guard = CreateGuard();
        const string connectionId = "conn-echo-5";
        int abortCount1 = 0;
        int abortCount2 = 0;

        guard.Track(connectionId, () => abortCount1++);
        guard.Track(connectionId, () => abortCount2++);

        Assert.Equal(2, _timeProvider.Timers.Count);
        RecordingTimerTimeProvider.RecordedTimer timer1 = _timeProvider.Timers[0];
        RecordingTimerTimeProvider.RecordedTimer timer2 = _timeProvider.Timers[1];

        Assert.False(timer1.IsDisposed);
        Assert.True(timer2.IsDisposed);

        // Fire timer 1's callback: original abort runs
        timer1.FireCallback();
        Assert.Equal(1, abortCount1);
        Assert.Equal(0, abortCount2);

        // Fire timer 2's callback: neither abort runs
        timer2.FireCallback();
        Assert.Equal(1, abortCount1);
        Assert.Equal(0, abortCount2);
    }

    [Fact]
    public void AbortAll_AbortsEveryPendingRegistrationOnce()
    {
        UnauthenticatedSocketGuard guard = CreateGuard();
        const string conn1 = "conn-foxtrot-6";
        const string conn2 = "conn-golf-7";
        int abortCount1 = 0;
        int abortCount2 = 0;

        guard.Track(conn1, () => abortCount1++);
        guard.Track(conn2, () => abortCount2++);

        RecordingTimerTimeProvider.RecordedTimer timer1 = _timeProvider.Timers[0];
        RecordingTimerTimeProvider.RecordedTimer timer2 = _timeProvider.Timers[1];

        guard.AbortAll();

        Assert.Equal(1, abortCount1);
        Assert.Equal(1, abortCount2);
        Assert.True(timer1.IsDisposed);
        Assert.True(timer2.IsDisposed);

        // Subsequent AbortAll or queued timer callbacks must not re-abort
        guard.AbortAll();
        timer1.FireCallback();
        timer2.FireCallback();

        Assert.Equal(1, abortCount1);
        Assert.Equal(1, abortCount2);
    }

    [Fact]
    public void Cleanup_UnknownIdsAndEmptyShutdownAreSafe()
    {
        UnauthenticatedSocketGuard guard = CreateGuard();
        const string trackedConn = "conn-hotel-8";
        int abortCount = 0;

        guard.Track(trackedConn, () => abortCount++);

        guard.MarkAuthenticated("unknown-conn-1");
        guard.Remove("unknown-conn-2");

        Assert.Equal(0, abortCount);

        UnauthenticatedSocketGuard emptyGuard = CreateGuard();
        emptyGuard.AbortAll();
    }

    private UnauthenticatedSocketGuard CreateGuard()
    {
        return new UnauthenticatedSocketGuard(_timeProvider, NullLogger<UnauthenticatedSocketGuard>.Instance);
    }
}
