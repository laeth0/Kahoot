namespace Kahoot.Application.IntegrationTests.TestSupport.Faults;

using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

public sealed class CommitFailureInterceptor : DbTransactionInterceptor
{
    public bool FailBeforeCommit { get; set; }

    public bool FailAfterCommit { get; set; }

    public Exception? InjectedException { get; set; }

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (FailBeforeCommit)
        {
            throw InjectedException ?? new InvalidOperationException("Simulated pre-commit transaction failure.");
        }

        return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
    }

    public override Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (FailAfterCommit)
        {
            throw InjectedException ?? new InvalidOperationException("Simulated post-commit transaction failure.");
        }

        return base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
    }
}
