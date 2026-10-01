namespace Kahoot.Infrastructure.UnitTests.Services;

using System;
using System.Linq;
using Kahoot.Infrastructure.Services;
using Kahoot.Infrastructure.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class CriticalWorkerFailureTrackerTests
{
    private readonly ManualTimeProvider _timeProvider = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly RecordingLogger<CriticalWorkerFailureTracker> _logger = new();

    [Fact]
    public void HasDegradedBacklog_NoFailuresReturnsFalseAndNullDetails()
    {
        CriticalWorkerFailureTracker tracker = CreateTracker(NullLogger<CriticalWorkerFailureTracker>.Instance);

        bool isDegraded = tracker.HasDegradedBacklog(out string? failingWorkerName, out TimeSpan? failureDuration);

        Assert.False(isDegraded);
        Assert.Null(failingWorkerName);
        Assert.Null(failureDuration);
    }

    [Fact]
    public void HasDegradedBacklog_DegradesAtExactlyFifteenMinutes()
    {
        CriticalWorkerFailureTracker tracker = CreateTracker(NullLogger<CriticalWorkerFailureTracker>.Instance);
        const string workerName = "GameAbandonmentWorker";
        InvalidOperationException exception = new("Database connection timeout");

        tracker.ReportFailure(workerName, exception);

        _timeProvider.Advance(TimeSpan.FromMinutes(15) - TimeSpan.FromTicks(1));
        bool isDegradedJustBefore = tracker.HasDegradedBacklog(out string? workerBefore, out TimeSpan? durationBefore);
        Assert.False(isDegradedJustBefore);
        Assert.Null(workerBefore);
        Assert.Null(durationBefore);

        _timeProvider.Advance(TimeSpan.FromTicks(1));
        bool isDegradedAtThreshold = tracker.HasDegradedBacklog(out string? workerAtThreshold, out TimeSpan? durationAtThreshold);
        Assert.True(isDegradedAtThreshold);
        Assert.Equal(workerName, workerAtThreshold);
        Assert.Equal(TimeSpan.FromMinutes(15), durationAtThreshold);
    }

    [Fact]
    public void ReportFailure_RetainsFirstFailureTimeAcrossRetries()
    {
        CriticalWorkerFailureTracker tracker = CreateTracker(NullLogger<CriticalWorkerFailureTracker>.Instance);
        const string workerName = "SuspensionFinalizerWorker";

        tracker.ReportFailure(workerName, new InvalidOperationException("First failure"));

        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        tracker.ReportFailure(workerName, new InvalidOperationException("Retry failure at t+10m"));

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        bool isDegraded = tracker.HasDegradedBacklog(out string? failingWorker, out TimeSpan? failureDuration);

        Assert.True(isDegraded);
        Assert.Equal(workerName, failingWorker);
        Assert.Equal(TimeSpan.FromMinutes(15), failureDuration);
    }

    [Fact]
    public void ReportSuccess_ResetsOnlyMatchingWorker()
    {
        CriticalWorkerFailureTracker tracker = CreateTracker(NullLogger<CriticalWorkerFailureTracker>.Instance);
        const string workerA = "RefreshTokenCleanupWorker";
        const string workerB = "QuestionImageCleanupWorker";

        tracker.ReportFailure(workerA, new InvalidOperationException("Worker A failure"));
        tracker.ReportFailure(workerB, new InvalidOperationException("Worker B failure"));

        // Case-insensitive reset for worker A
        tracker.ReportSuccess("refreshtokencleanupworker");

        _timeProvider.Advance(TimeSpan.FromMinutes(15));
        bool isDegraded = tracker.HasDegradedBacklog(out string? failingWorker, out TimeSpan? failureDuration);

        Assert.True(isDegraded);
        Assert.Equal(workerB, failingWorker);
        Assert.Equal(TimeSpan.FromMinutes(15), failureDuration);
    }

    [Fact]
    public void ReportSuccess_NewFailureStartsFreshWindow()
    {
        CriticalWorkerFailureTracker tracker = CreateTracker(NullLogger<CriticalWorkerFailureTracker>.Instance);
        const string workerName = "GameAbandonmentWorker";

        tracker.ReportFailure(workerName, new InvalidOperationException("Initial failure"));

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        tracker.ReportSuccess(workerName);

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        tracker.ReportFailure(workerName, new InvalidOperationException("New failure after recovery"));

        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        bool isDegradedBeforeThreshold = tracker.HasDegradedBacklog(out string? workerBefore, out TimeSpan? durationBefore);
        Assert.False(isDegradedBeforeThreshold);
        Assert.Null(workerBefore);
        Assert.Null(durationBefore);

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        bool isDegradedAtThreshold = tracker.HasDegradedBacklog(out string? workerAtThreshold, out TimeSpan? durationAtThreshold);
        Assert.True(isDegradedAtThreshold);
        Assert.Equal(workerName, workerAtThreshold);
        Assert.Equal(TimeSpan.FromMinutes(15), durationAtThreshold);
    }

    [Fact]
    public void ReportFailure_LogsWarningThenEscalatesAtThreshold()
    {
        CriticalWorkerFailureTracker tracker = CreateTracker(_logger);
        const string workerName = "CriticalTaskWorker";
        InvalidOperationException firstException = new("First attempt failure");
        InvalidOperationException secondException = new("Second attempt failure");

        tracker.ReportFailure(workerName, firstException);

        Assert.Single(_logger.Entries);
        RecordedLogEntry warningEntry = _logger.Entries[0];
        Assert.Equal(LogLevel.Warning, warningEntry.LogLevel);
        Assert.Same(firstException, warningEntry.Exception);
        Assert.Equal("CriticalWorkerAttemptFailed", warningEntry.GetValue("EventName"));
        Assert.Equal(workerName, warningEntry.GetValue("WorkerName"));

        _timeProvider.Advance(TimeSpan.FromMinutes(15));
        tracker.ReportFailure(workerName, secondException);

        Assert.Equal(2, _logger.Entries.Count);
        RecordedLogEntry errorEntry = _logger.Entries[1];
        Assert.Equal(LogLevel.Error, errorEntry.LogLevel);
        Assert.Same(secondException, errorEntry.Exception);
        Assert.Equal("CriticalWorkerBacklogEscalated", errorEntry.GetValue("EventName"));
        Assert.Equal(workerName, errorEntry.GetValue("WorkerName"));
        Assert.Equal(2, errorEntry.GetValue("FailureCount"));
    }

    [Fact]
    public void ReportSuccess_LogsRecoveryOnlyForTrackedWorker()
    {
        CriticalWorkerFailureTracker tracker = CreateTracker(_logger);
        const string workerName = "TrackedWorker";

        tracker.ReportSuccess("UnknownUntrackedWorker");
        Assert.Empty(_logger.Entries);

        tracker.ReportFailure(workerName, new InvalidOperationException("Failure before recovery"));
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        tracker.ReportSuccess(workerName);

        RecordedLogEntry? recoveryEntry = _logger.Entries.FirstOrDefault(entry => entry.LogLevel == LogLevel.Information);
        Assert.NotNull(recoveryEntry);
        Assert.Equal("CriticalWorkerRecovered", recoveryEntry.GetValue("EventName"));
        Assert.Equal(workerName, recoveryEntry.GetValue("WorkerName"));
        Assert.Equal(5.0, recoveryEntry.GetValue("DurationMinutes"));
    }

    private CriticalWorkerFailureTracker CreateTracker(ILogger<CriticalWorkerFailureTracker> logger)
    {
        return new CriticalWorkerFailureTracker(_timeProvider, logger);
    }
}
