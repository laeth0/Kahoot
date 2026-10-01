namespace Kahoot.Application.IntegrationTests.TestSupport.Concurrency;

using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

public sealed class TransactionCommitInterceptor : DbTransactionInterceptor
{
    private readonly ScopeConcurrencyGate _scopeGate;

    public TransactionCommitInterceptor(ScopeConcurrencyGate scopeGate)
    {
        _scopeGate = scopeGate;
    }

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (_scopeGate.CommitGate is not null)
        {
            _scopeGate.CommitGate.SignalReached();
            await _scopeGate.CommitGate.AwaitReleaseAsync(cancellationToken);
        }

        if (_scopeGate.FailBeforeCommit)
        {
            throw _scopeGate.CustomCommitException ?? new InvalidOperationException("Simulated pre-commit database failure.");
        }

        return await base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
    }

    public override async Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (_scopeGate.FailAfterCommit)
        {
            throw _scopeGate.CustomCommitException ?? new InvalidOperationException("Simulated post-commit failure.");
        }

        await base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
    }
}
