namespace Kahoot.Infrastructure.Realtime;

using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

public sealed class HostPresenceService
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan KeyRetention = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, HostConnection> _connections = new();
    private readonly IDatabase _database;
    private readonly string _keyPrefix;
    private readonly string _instanceId = Guid.NewGuid().ToString("N");
    private readonly TimeProvider _timeProvider;

    public HostPresenceService(
        IConnectionMultiplexer redis,
        IOptions<RealtimeOptions> options,
        TimeProvider timeProvider)
    {
        _database = redis.GetDatabase();
        _keyPrefix = options.Value.ChannelPrefix;
        _timeProvider = timeProvider;
    }

    public bool TryGetConnection(string connectionId, out Guid hostAccountId, out Guid gameId)
    {
        if (_connections.TryGetValue(connectionId, out HostConnection? connection))
        {
            hostAccountId = connection.HostAccountId;
            gameId = connection.GameId;
            return true;
        }

        hostAccountId = default;
        gameId = default;
        return false;
    }

    public async Task RegisterAsync(
        string connectionId, Guid hostAccountId, Guid gameId, int tokenSecurityVersion, Action abortConnection)
    {
        if (_connections.TryGetValue(connectionId, out HostConnection? existing))
        {
            if (existing.HostAccountId != hostAccountId || existing.GameId != gameId ||
                existing.TokenSecurityVersion != tokenSecurityVersion)
            {
                throw new InvalidOperationException("A Host connection cannot attach to multiple games.");
            }

            await existing.Gate.WaitAsync();
            try
            {
                if (_connections.TryGetValue(connectionId, out HostConnection? current) &&
                    ReferenceEquals(current, existing))
                {
                    await RefreshAsync(connectionId, existing);
                }
            }
            finally
            {
                existing.Gate.Release();
            }

            return;
        }

        HostConnection connection = new HostConnection(hostAccountId, gameId, tokenSecurityVersion, abortConnection);
        if (!_connections.TryAdd(connectionId, connection))
        {
            throw new InvalidOperationException("A Host connection was registered concurrently.");
        }

        await connection.Gate.WaitAsync();
        try
        {
            await RefreshAsync(connectionId, connection);
        }
        catch
        {
            _connections.TryRemove(connectionId, out _);
            throw;
        }
        finally
        {
            connection.Gate.Release();
        }
    }

    public async Task<bool> RemoveAsync(string connectionId, Guid gameId)
    {
        if (!_connections.TryGetValue(connectionId, out HostConnection? connection))
        {
            return await HasAnyAsync(gameId);
        }

        await connection.Gate.WaitAsync();
        try
        {
            if (_connections.TryRemove(connectionId, out _))
            {
                await _database.SortedSetRemoveAsync(Key(connection.GameId), Member(connectionId));
            }
        }
        finally
        {
            connection.Gate.Release();
        }

        return await HasAnyAsync(connection.GameId);
    }

    public async Task<bool> HasAnyAsync(Guid gameId)
    {
        RedisKey key = Key(gameId);
        long now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        await _database.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, now);
        return await _database.SortedSetLengthAsync(key) > 0;
    }

    public async Task RenewAllAsync(CancellationToken cancellationToken)
    {
        foreach (KeyValuePair<string, HostConnection> entry in _connections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await entry.Value.Gate.WaitAsync(cancellationToken);
            try
            {
                if (_connections.TryGetValue(entry.Key, out HostConnection? current) &&
                    ReferenceEquals(current, entry.Value))
                {
                    await RefreshAsync(entry.Key, entry.Value);
                }
            }
            finally
            {
                entry.Value.Gate.Release();
            }
        }
    }

    public void AbortHostConnections(Guid hostAccountId)
    {
        foreach (HostConnection connection in _connections.Values)
        {
            if (connection.HostAccountId == hostAccountId)
            {
                connection.Abort();
            }
        }
    }

    public Guid[] GetConnectedHostAccountIds() => _connections.Values
        .Select(connection => connection.HostAccountId)
        .Distinct()
        .ToArray();

    public void AbortInvalidConnections(IReadOnlyDictionary<Guid, int> activeVersions)
    {
        foreach (HostConnection connection in _connections.Values)
        {
            if (!activeVersions.TryGetValue(connection.HostAccountId, out int currentVersion) ||
                connection.TokenSecurityVersion != currentVersion)
            {
                connection.Abort();
            }
        }
    }

    private async Task RefreshAsync(string connectionId, HostConnection connection)
    {
        RedisKey key = Key(connection.GameId);
        long expiresAt = _timeProvider.GetUtcNow().Add(LeaseDuration).ToUnixTimeMilliseconds();
        await _database.SortedSetAddAsync(key, Member(connectionId), expiresAt);
        await _database.KeyExpireAsync(key, KeyRetention);
    }

    private RedisKey Key(Guid gameId) => $"{_keyPrefix}:game:{gameId:N}:hosts";

    private RedisValue Member(string connectionId) => $"{_instanceId}:{connectionId}";

    private sealed class HostConnection
    {
        public HostConnection(Guid hostAccountId, Guid gameId, int tokenSecurityVersion, Action abort)
        {
            HostAccountId = hostAccountId;
            GameId = gameId;
            TokenSecurityVersion = tokenSecurityVersion;
            Abort = abort;
        }

        public Guid HostAccountId { get; }

        public Guid GameId { get; }

        public int TokenSecurityVersion { get; }

        public Action Abort { get; }

        public SemaphoreSlim Gate { get; } = new SemaphoreSlim(1, 1);
    }
}
