namespace Kahoot.Application.Common.Interfaces;

// Critical Worker Failure Tracker - Tracks background worker health and triggers readiness degradation when worker backlogs persist (OPS-WORK-002).
public interface ICriticalWorkerFailureTracker
{
    // Record Successful Execution - Clears any existing failure state and logs worker recovery.
    void ReportSuccess(string workerName);

    // Record Worker Failure - Records failure timestamp and emits high-priority escalation when failure duration exceeds 15 minutes.
    void ReportFailure(string workerName, Exception exception);

    // Backlog Health Evaluation - Evaluates whether any critical worker has been failing continuously beyond the 15-minute threshold.
    bool HasDegradedBacklog(out string? failingWorkerName, out TimeSpan? failureDuration);
}
