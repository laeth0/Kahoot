namespace Kahoot.Application.IntegrationTests.TestSupport;

using Kahoot.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

public sealed class ApplicationRequestScope : IAsyncDisposable, IDisposable
{
    private readonly AsyncServiceScope _scope;
    private bool _disposed;

    public ApplicationRequestScope(AsyncServiceScope scope, TestCaller caller)
    {
        _scope = scope;
        Caller = caller;

        TestCallerHolder holder = _scope.ServiceProvider.GetRequiredService<TestCallerHolder>();
        holder.Caller = caller;

        Sender = _scope.ServiceProvider.GetRequiredService<ISender>();
        DbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    public ISender Sender { get; }

    public AppDbContext DbContext { get; }

    public IServiceProvider Services => _scope.ServiceProvider;

    public TestCaller Caller { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scope.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _scope.DisposeAsync();
    }
}
