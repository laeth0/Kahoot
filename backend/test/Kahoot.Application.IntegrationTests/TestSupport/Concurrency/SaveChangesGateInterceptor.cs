namespace Kahoot.Application.IntegrationTests.TestSupport.Concurrency;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

public sealed class SaveChangesGateInterceptor : SaveChangesInterceptor
{
    private readonly ScopeConcurrencyGate _scopeGate;

    public SaveChangesGateInterceptor(ScopeConcurrencyGate scopeGate)
    {
        _scopeGate = scopeGate;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (_scopeGate.SaveGate is not null)
        {
            _scopeGate.SaveGate.SignalReached();
            await _scopeGate.SaveGate.AwaitReleaseAsync(cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
