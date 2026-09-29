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

    // Per-IP Rate Limiting (Dimension 1) - Max 30 attempts per minute per IP to block high-frequency automated scripts
    public bool IsIpRateLimited(string ipAddress)
    {
        string key = string.IsNullOrWhiteSpace(ipAddress) ? "unknown" : ipAddress.Trim();
        IpAttemptTracker tracker = _ipTrackers.GetOrAdd(key, _ => new IpAttemptTracker());
        DateTimeOffset now = _timeProvider.GetUtcNow();

        return !tracker.TryRecordAttempt(now, MaxAttemptsPerIp, IpWindow);
    }

    // Progressive Exponential Backoff (Dimension 2) - Delays login (1s to 10s) after 5 failures without locking out legitimate users
    public TimeSpan GetUsernameBackoffDelay(string normalizedUsername)
    {
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return TimeSpan.Zero;
        }

        if (!_usernameBackoffs.TryGetValue(normalizedUsername, out UsernameBackoffState? state))
        {
            return TimeSpan.Zero;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
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
            int seconds = 1 << backoffStep;
            return TimeSpan.FromSeconds(Math.Min(seconds, 10));
        }
    }

    public void RecordFailedAttempt(string normalizedUsername)
    {
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return;
        }

        UsernameBackoffState state = _usernameBackoffs.GetOrAdd(normalizedUsername, _ => new UsernameBackoffState());
        DateTimeOffset now = _timeProvider.GetUtcNow();
        lock (state)
        {
            if (now - state.LastFailedAt > FailedAttemptWindow)
            {
                state.FailedCount = 0;
            }

            // Counts above nine all use the same 10-second cap.
            state.FailedCount = Math.Min(state.FailedCount + 1, 9);
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
                DateTimeOffset cutoff = now - window;
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
