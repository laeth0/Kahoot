namespace Kahoot.Infrastructure.UnitTests.TestSupport;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class RecordingTimerTimeProvider : TimeProvider
{
    private readonly ConcurrentQueue<RecordedTimer> _timers = new();

    public IReadOnlyList<RecordedTimer> Timers => _timers.ToArray();

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        RecordedTimer timer = new(callback, state, dueTime, period);
        _timers.Enqueue(timer);
        return timer;
    }

    public sealed class RecordedTimer : ITimer
    {
        public TimerCallback Callback { get; }
        public object? State { get; }
        public TimeSpan DueTime { get; private set; }
        public TimeSpan Period { get; private set; }
        public bool IsDisposed { get; private set; }
        public int DisposeCount { get; private set; }

        public RecordedTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            Callback = callback;
            State = state;
            DueTime = dueTime;
            Period = period;
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            DueTime = dueTime;
            Period = period;
            return true;
        }

        public void Dispose()
        {
            IsDisposed = true;
            DisposeCount++;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public void FireCallback()
        {
            Callback(State);
        }
    }
}
