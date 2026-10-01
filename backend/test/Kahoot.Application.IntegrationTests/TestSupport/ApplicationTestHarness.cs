namespace Kahoot.Application.IntegrationTests.TestSupport;

using System.Net;
using Kahoot.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

public sealed class ApplicationTestHarness : IAsyncDisposable
{
    private readonly DatabaseSandbox _databaseSandbox;
    private readonly ServiceProvider _serviceProvider;
    private readonly string _redisChannelPrefix;
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private bool _disposed;

    public ApplicationTestHarness(
        DatabaseSandbox databaseSandbox,
        ServiceProvider serviceProvider,
        string redisChannelPrefix,
        IConnectionMultiplexer connectionMultiplexer)
    {
        _databaseSandbox = databaseSandbox;
        _serviceProvider = serviceProvider;
        _redisChannelPrefix = redisChannelPrefix;
        _connectionMultiplexer = connectionMultiplexer;
    }

    public DatabaseSandbox Sandbox => _databaseSandbox;

    public string ConnectionString => _databaseSandbox.ConnectionString;

    public IServiceProvider Services => _serviceProvider;

    public string RedisChannelPrefix => _redisChannelPrefix;

    public string? OwnedDirectoryPath { get; set; }

    public ApplicationRequestScope CreateRequestScope(TestCaller caller)
    {
        AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
        return new ApplicationRequestScope(scope, caller);
    }

    public async Task<TResponse> SendAsync<TResponse>(
        IRequest<TResponse> request,
        TestCaller caller,
        CancellationToken cancellationToken = default)
    {
        await using ApplicationRequestScope scope = CreateRequestScope(caller);
        return await scope.Sender.Send(request, cancellationToken);
    }

    public async Task<T> ReadDbAsync<T>(
        Func<AppDbContext, CancellationToken, Task<T>> read,
        CancellationToken cancellationToken = default)
    {
        await using ApplicationRequestScope scope = CreateRequestScope(TestCaller.Anonymous);
        return await read(scope.DbContext, cancellationToken);
    }

    public async Task<T> ReadDbAsync<T>(
        Func<AppDbContext, Task<T>> read)
    {
        await using ApplicationRequestScope scope = CreateRequestScope(TestCaller.Anonymous);
        return await read(scope.DbContext);
    }

    public async Task ReadDbAsync(
        Func<AppDbContext, CancellationToken, Task> read,
        CancellationToken cancellationToken = default)
    {
        await using ApplicationRequestScope scope = CreateRequestScope(TestCaller.Anonymous);
        await read(scope.DbContext, cancellationToken);
    }

    public async Task ReadDbAsync(
        Func<AppDbContext, Task> read)
    {
        await using ApplicationRequestScope scope = CreateRequestScope(TestCaller.Anonymous);
        await read(scope.DbContext);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            await _serviceProvider.DisposeAsync();
        }
        catch
        {
            // Suppress exception during provider disposal to preserve test outcome
        }

        try
        {
            EndPoint[] endpoints = _connectionMultiplexer.GetEndPoints();
            foreach (EndPoint endpoint in endpoints)
            {
                IServer server = _connectionMultiplexer.GetServer(endpoint);
                if (!server.IsReplica)
                {
                    RedisKey[] keys = server.Keys(pattern: $"{_redisChannelPrefix}*").ToArray();
                    if (keys.Length > 0)
                    {
                        IDatabase db = _connectionMultiplexer.GetDatabase();
                        await db.KeyDeleteAsync(keys);
                    }
                }
            }
        }
        catch
        {
            // Suppress exception during Redis key cleanup
        }

        if (OwnedDirectoryPath is not null && Directory.Exists(OwnedDirectoryPath))
        {
            try
            {
                Directory.Delete(OwnedDirectoryPath, true);
            }
            catch
            {
                // Suppress exception during directory cleanup
            }
        }

        await _databaseSandbox.DisposeAsync();
    }
}
