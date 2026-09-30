namespace Kahoot.Infrastructure.Services;

using System.Collections.Concurrent;
using Kahoot.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

// Critical Worker Failure Tracker - Escalates worker errors to degraded readiness when backlog persists beyond 15 minutes (OPS-WORK-002).
public sealed class CriticalWorkerFailureTracker : ICriticalWorkerFailureTracker
{
    private static readonly TimeSpan DegradationThreshold = TimeSpan.FromMinutes(15);

    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CriticalWorkerFailureTracker> _logger;
    private readonly ConcurrentDictionary<string, WorkerFailureState> _workerStates = new(StringComparer.OrdinalIgnoreCase);

    public CriticalWorkerFailureTracker(
        TimeProvider timeProvider,
        ILogger<CriticalWorkerFailureTracker> logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // Health Recovery Notification - Resets failure state when a worker pass finishes without unhandled exceptions.
    public void ReportSuccess(string workerName)
    {
        if (_workerStates.TryRemove(workerName, out WorkerFailureState? previousState))
        {
            TimeSpan duration = _timeProvider.GetUtcNow() - previousState.FirstFailureUtc;
            _logger.LogInformation(
                "Critical worker {WorkerName} recovered after failing for {DurationMinutes:F1} minutes. EventName={EventName}",
                workerName, duration.TotalMinutes, "CriticalWorkerRecovered");
        }
    }

    // Failure Escalation Reporting - Logs warning on transient failures and escalates to high-priority error beyond 15 minutes.
    public void ReportFailure(string workerName, Exception exception)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        WorkerFailureState state = _workerStates.AddOrUpdate(
            workerName,
            _ => new WorkerFailureState(now, 1),
            (_, existing) => new WorkerFailureState(existing.FirstFailureUtc, existing.FailureCount + 1));

        TimeSpan failureDuration = now - state.FirstFailureUtc;
        if (failureDuration >= DegradationThreshold)
        {
            // High-Priority Operational Escalation (OPS-WORK-002) - Emits high-priority error when critical worker backlog persists beyond 15 minutes
            _logger.LogError(
                exception,
                "HIGH PRIORITY OPERATIONAL ALERT: Critical worker {WorkerName} backlog has persisted for {DurationMinutes:F1} minutes ({FailureCount} failures). Readiness marked DEGRADED. EventName={EventName}",
                workerName, failureDuration.TotalMinutes, state.FailureCount, "CriticalWorkerBacklogEscalated");
        }
        else
        {
            _logger.LogWarning(
                exception,
                "Critical worker {WorkerName} attempt failed. Failure duration: {DurationMinutes:F1} minutes. EventName={EventName}",
                workerName, failureDuration.TotalMinutes, "CriticalWorkerAttemptFailed");
        }
    }

    // Backlog Health Evaluation - Queries whether any critical worker has exceeded the 15-minute continuous failure ceiling.
    public bool HasDegradedBacklog(out string? failingWorkerName, out TimeSpan? failureDuration)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        foreach (KeyValuePair<string, WorkerFailureState> entry in _workerStates)
        {
            TimeSpan duration = now - entry.Value.FirstFailureUtc;
            if (duration >= DegradationThreshold)
            {
                failingWorkerName = entry.Key;
                failureDuration = duration;
                return true;
            }
        }

        failingWorkerName = null;
        failureDuration = null;
        return false;
    }

    private sealed record WorkerFailureState(DateTimeOffset FirstFailureUtc, int FailureCount);
}
