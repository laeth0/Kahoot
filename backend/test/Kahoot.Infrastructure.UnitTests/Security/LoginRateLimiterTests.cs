using Kahoot.Infrastructure.Security;
using Kahoot.Infrastructure.UnitTests.TestSupport;
using Xunit;

namespace Kahoot.Infrastructure.UnitTests.Security;

public sealed class LoginRateLimiterTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsIpRateLimited_AllowsThirtyAttemptsThenRejects()
    {
        ManualTimeProvider clock = new(BaseTime);
        LoginRateLimiter limiter = new(clock);
        const string ipAddress = "192.168.1.100";

        for (int attempt = 1; attempt <= 30; attempt++)
        {
            bool isLimited = limiter.IsIpRateLimited(ipAddress);
            Assert.False(isLimited);
        }

        bool thirtyFirstAttempt = limiter.IsIpRateLimited(ipAddress);
        Assert.True(thirtyFirstAttempt);
    }

    [Fact]
    public void IsIpRateLimited_ExpiresAttemptsAtExactlyOneMinute()
    {
        ManualTimeProvider clock1 = new(BaseTime);
        LoginRateLimiter limiter1 = new(clock1);
        const string ipAddress = "10.0.0.1";

        for (int attempt = 1; attempt <= 30; attempt++)
        {
            limiter1.IsIpRateLimited(ipAddress);
        }

        // At 59.9999999s (60 seconds minus 1 tick), attempts have not expired yet
        clock1.Advance(TimeSpan.FromSeconds(60) - TimeSpan.FromTicks(1));
        bool isLimitedBeforeExpiry = limiter1.IsIpRateLimited(ipAddress);
        Assert.True(isLimitedBeforeExpiry);

        // At exactly 60 seconds from initial attempts, the attempts expire
        ManualTimeProvider clock2 = new(BaseTime);
        LoginRateLimiter limiter2 = new(clock2);

        for (int attempt = 1; attempt <= 30; attempt++)
        {
            limiter2.IsIpRateLimited(ipAddress);
        }

        clock2.Advance(TimeSpan.FromSeconds(60));
        bool isLimitedAtExactMinute = limiter2.IsIpRateLimited(ipAddress);
        Assert.False(isLimitedAtExactMinute);
    }

    [Fact]
    public void IsIpRateLimited_UsesRollingWindow()
    {
        ManualTimeProvider clock = new(BaseTime);
        LoginRateLimiter limiter = new(clock);
        const string ipAddress = "172.16.0.5";

        // 15 attempts at t = 0
        for (int attempt = 1; attempt <= 15; attempt++)
        {
            Assert.False(limiter.IsIpRateLimited(ipAddress));
        }

        // Advance to t = 30s, record 15 more attempts (total 30)
        clock.Advance(TimeSpan.FromSeconds(30));
        for (int attempt = 16; attempt <= 30; attempt++)
        {
            Assert.False(limiter.IsIpRateLimited(ipAddress));
        }

        // At t = 30s, budget is exhausted
        Assert.True(limiter.IsIpRateLimited(ipAddress));

        // Advance to t = 60s: first 15 attempts expire
        clock.Advance(TimeSpan.FromSeconds(30));

        // Exactly 15 new attempts can be admitted
        for (int attempt = 1; attempt <= 15; attempt++)
        {
            Assert.False(limiter.IsIpRateLimited(ipAddress));
        }

        // 16th is rejected because the second batch (at t=30s) hasn't expired yet
        Assert.True(limiter.IsIpRateLimited(ipAddress));
    }

    [Fact]
    public void IsIpRateLimited_IsolatesAndNormalizesIpKeys()
    {
        ManualTimeProvider clock = new(BaseTime);
        LoginRateLimiter limiter = new(clock);

        // IP 1 fills budget
        for (int attempt = 1; attempt <= 30; attempt++)
        {
            limiter.IsIpRateLimited("192.168.1.1");
        }
        Assert.True(limiter.IsIpRateLimited("192.168.1.1"));

        // IP 2 has separate budget
        Assert.False(limiter.IsIpRateLimited("192.168.1.2"));

        // Padded IP text shares budget with trimmed form
        for (int attempt = 1; attempt <= 30; attempt++)
        {
            limiter.IsIpRateLimited(" 10.10.10.10 ");
        }
        Assert.True(limiter.IsIpRateLimited("10.10.10.10"));

        // Whitespace and empty share the unknown bucket
        for (int attempt = 1; attempt <= 30; attempt++)
        {
            limiter.IsIpRateLimited(string.Empty);
        }
        Assert.True(limiter.IsIpRateLimited("   "));
    }

    [Fact]
    public void IsIpRateLimited_RejectionDoesNotExtendWindow()
    {
        ManualTimeProvider clock = new(BaseTime);
        LoginRateLimiter limiter = new(clock);
        const string ipAddress = "192.168.1.200";

        for (int attempt = 1; attempt <= 30; attempt++)
        {
            limiter.IsIpRateLimited(ipAddress);
        }

        // Rejection at t = 30s
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.True(limiter.IsIpRateLimited(ipAddress));

        // Advance to t = 60s (60s after original 30 attempts)
        clock.Advance(TimeSpan.FromSeconds(30));

        // Should be admitted because the rejection at t=30s did not extend the window
        Assert.False(limiter.IsIpRateLimited(ipAddress));
    }

    [Fact]
    public async Task IsIpRateLimited_ConcurrentAttemptsAdmitExactlyThirty()
    {
        ManualTimeProvider clock = new(BaseTime);
        LoginRateLimiter limiter = new(clock);
        const string ipAddress = "192.168.1.250";

        TaskCompletionSource startSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        const int totalAttempts = 64;
        Task<bool>[] tasks = new Task<bool>[totalAttempts];

        for (int index = 0; index < totalAttempts; index++)
        {
            tasks[index] = Task.Run(async () =>
            {
                await startSignal.Task;
                return limiter.IsIpRateLimited(ipAddress);
            });
        }

        startSignal.SetResult();
        bool[] results = await Task.WhenAll(tasks);

        int admittedCount = results.Count(isLimited => !isLimited);
        int rejectedCount = results.Count(isLimited => isLimited);

        Assert.Equal(30, admittedCount);
        Assert.Equal(34, rejectedCount);
    }

    [Fact]
    public void GetUsernameBackoffDelay_FollowsProgression()
    {
        ManualTimeProvider clock = new(BaseTime);
        LoginRateLimiter limiter = new(clock);
        const string username = "BOB_TEST";

        // Failures 0 through 4 yield zero delay
        for (int failure = 0; failure < 4; failure++)
        {
            limiter.RecordFailedAttempt(username);
            Assert.Equal(TimeSpan.Zero, limiter.GetUsernameBackoffDelay(username));
        }

        // 5th failure: 1s
        limiter.RecordFailedAttempt(username);
        Assert.Equal(TimeSpan.FromSeconds(1), limiter.GetUsernameBackoffDelay(username));

        // 6th failure: 2s
        limiter.RecordFailedAttempt(username);
        Assert.Equal(TimeSpan.FromSeconds(2), limiter.GetUsernameBackoffDelay(username));

        // 7th failure: 4s
        limiter.RecordFailedAttempt(username);
        Assert.Equal(TimeSpan.FromSeconds(4), limiter.GetUsernameBackoffDelay(username));

        // 8th failure: 8s
        limiter.RecordFailedAttempt(username);
        Assert.Equal(TimeSpan.FromSeconds(8), limiter.GetUsernameBackoffDelay(username));

        // 9th failure: 10s (capped)
        limiter.RecordFailedAttempt(username);
        Assert.Equal(TimeSpan.FromSeconds(10), limiter.GetUsernameBackoffDelay(username));

        // Subsequent failures (10 through 15) remain capped at 10s without shift overflow
        for (int extra = 10; extra <= 15; extra++)
        {
            limiter.RecordFailedAttempt(username);
            Assert.Equal(TimeSpan.FromSeconds(10), limiter.GetUsernameBackoffDelay(username));
        }
    }

    [Fact]
    public void GetUsernameBackoffDelay_ExpiresStrictlyAfterFifteenMinutes()
    {
        ManualTimeProvider clock = new(BaseTime);
        LoginRateLimiter limiter = new(clock);
        const string username = "ALICE_TEST";

        for (int failure = 1; failure <= 5; failure++)
        {
            limiter.RecordFailedAttempt(username);
        }

        Assert.Equal(TimeSpan.FromSeconds(1), limiter.GetUsernameBackoffDelay(username));

        // At exactly 15 minutes, it is not strictly greater than 15 minutes, so delay is still 1s
        clock.Advance(TimeSpan.FromMinutes(15));
        Assert.Equal(TimeSpan.FromSeconds(1), limiter.GetUsernameBackoffDelay(username));

        // At 15 minutes plus one tick, now - LastFailedAt > 15m, so delay expires to zero
        clock.Advance(TimeSpan.FromTicks(1));
        Assert.Equal(TimeSpan.Zero, limiter.GetUsernameBackoffDelay(username));
    }

    [Fact]
    public void RecordFailedAttempt_RenewsWindowAndRestartsExpiredCount()
    {
        ManualTimeProvider clock = new(BaseTime);
        LoginRateLimiter limiter = new(clock);
        const string username = "CHARLIE_TEST";

        for (int failure = 1; failure <= 5; failure++)
        {
            limiter.RecordFailedAttempt(username);
        }

        // Advance 10 minutes, record another failure -> moves anchor to t+10m
        clock.Advance(TimeSpan.FromMinutes(10));
        limiter.RecordFailedAttempt(username);
        Assert.Equal(TimeSpan.FromSeconds(2), limiter.GetUsernameBackoffDelay(username));

        // At t+20m (10m after last failure), window has NOT expired yet
        clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(TimeSpan.FromSeconds(2), limiter.GetUsernameBackoffDelay(username));

        // Advance past 15m from the second anchor (advance 5m + 1 tick)
        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromTicks(1));
        Assert.Equal(TimeSpan.Zero, limiter.GetUsernameBackoffDelay(username));

        // A new failure now starts a fresh count of 1 (delay = 0 since count < 5)
        limiter.RecordFailedAttempt(username);
        Assert.Equal(TimeSpan.Zero, limiter.GetUsernameBackoffDelay(username));
    }

    [Fact]
    public void ResetFailedAttempts_ClearsOnlySelectedUsername()
    {
        ManualTimeProvider clock = new(BaseTime);
        LoginRateLimiter limiter = new(clock);
        const string user1 = "USER_ONE";
        const string user2 = "USER_TWO";

        for (int failure = 1; failure <= 5; failure++)
        {
            limiter.RecordFailedAttempt(user1);
            limiter.RecordFailedAttempt(user2);
        }

        Assert.Equal(TimeSpan.FromSeconds(1), limiter.GetUsernameBackoffDelay(user1));
        Assert.Equal(TimeSpan.FromSeconds(1), limiter.GetUsernameBackoffDelay(user2));

        limiter.ResetFailedAttempts(user1);

        Assert.Equal(TimeSpan.Zero, limiter.GetUsernameBackoffDelay(user1));
        Assert.Equal(TimeSpan.FromSeconds(1), limiter.GetUsernameBackoffDelay(user2));
    }

    [Fact]
    public void UsernameBackoff_IgnoresBlankNamesAndKeepsKeysIndependent()
    {
        ManualTimeProvider clock = new(BaseTime);
        LoginRateLimiter limiter = new(clock);

        limiter.RecordFailedAttempt(string.Empty);
        limiter.RecordFailedAttempt("   ");
        limiter.ResetFailedAttempts(string.Empty);

        Assert.Equal(TimeSpan.Zero, limiter.GetUsernameBackoffDelay(string.Empty));
        Assert.Equal(TimeSpan.Zero, limiter.GetUsernameBackoffDelay("   "));

        // Distinct normalized keys remain independent
        limiter.RecordFailedAttempt("USER_A");
        Assert.Equal(TimeSpan.Zero, limiter.GetUsernameBackoffDelay("USER_B"));
    }
}
