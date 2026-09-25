using System.Collections.Concurrent;
using Kahoot.Application.Common.Interfaces;

namespace Kahoot.Infrastructure.Security;

public sealed class LoginRateLimiter : ILoginRateLimiter
{
    private const int MaxAttemptsPerIp = 30;
    private static readonly TimeSpan IpWindow = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan FailedAttemptWindow = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, IpAttemptTracker> _ipTrackers = new();
    private readonly ConcurrentDictionary<string, UsernameBackoffState> _usernameBackoffs = new();
    private readonly TimeProvider _timeProvider;

    public LoginRateLimiter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public bool IsIpRateLimited(string ipAddress)
    {
        var key = string.IsNullOrWhiteSpace(ipAddress) ? "unknown" : ipAddress.Trim();
        var tracker = _ipTrackers.GetOrAdd(key, _ => new IpAttemptTracker());
        var now = _timeProvider.GetUtcNow();

        return !tracker.TryRecordAttempt(now, MaxAttemptsPerIp, IpWindow);
    }

    public TimeSpan GetUsernameBackoffDelay(string normalizedUsername)
    {
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return TimeSpan.Zero;
        }

        if (!_usernameBackoffs.TryGetValue(normalizedUsername, out var state))
        {
            return TimeSpan.Zero;
        }

        var now = _timeProvider.GetUtcNow();
        lock (state)
        {
            if (now - state.LastFailedAt > FailedAttemptWindow)
            {
                state.FailedCount = 0;
                return TimeSpan.Zero;
            }

            if (state.FailedCount < 5)
            {
                return TimeSpan.Zero;
            }

            int backoffStep = state.FailedCount - 5;
            int seconds = (int)Math.Pow(2, backoffStep);
            return TimeSpan.FromSeconds(Math.Min(seconds, 10));
        }
    }

    public void RecordFailedAttempt(string normalizedUsername)
    {
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return;
        }

        var state = _usernameBackoffs.GetOrAdd(normalizedUsername, _ => new UsernameBackoffState());
        var now = _timeProvider.GetUtcNow();
        lock (state)
        {
            if (now - state.LastFailedAt > FailedAttemptWindow)
            {
                state.FailedCount = 0;
            }

            state.FailedCount++;
            state.LastFailedAt = now;
        }
    }

    public void ResetFailedAttempts(string normalizedUsername)
    {
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return;
        }

        _usernameBackoffs.TryRemove(normalizedUsername, out _);
    }

    private sealed class IpAttemptTracker
    {
        private readonly object _lock = new();
        private readonly Queue<DateTimeOffset> _timestamps = new();

        public bool TryRecordAttempt(DateTimeOffset now, int maxAttempts, TimeSpan window)
        {
            lock (_lock)
            {
                var cutoff = now - window;
                while (_timestamps.Count > 0 && _timestamps.Peek() <= cutoff)
                {
                    _timestamps.Dequeue();
                }

                if (_timestamps.Count >= maxAttempts)
                {
                    return false;
                }

                _timestamps.Enqueue(now);
                return true;
            }
        }
    }

    private sealed class UsernameBackoffState
    {
        public int FailedCount { get; set; }
        public DateTimeOffset LastFailedAt { get; set; }
    }
}
