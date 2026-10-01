namespace Kahoot.Application.IntegrationTests.TestSupport.Concurrency;

using System;

public sealed class ScopeConcurrencyGate
{
    public TransactionCommitGate? CommitGate { get; set; }

    public SaveChangesGate? SaveGate { get; set; }

    public bool FailBeforeCommit { get; set; }

    public bool FailAfterCommit { get; set; }

    public Exception? CustomCommitException { get; set; }
}
