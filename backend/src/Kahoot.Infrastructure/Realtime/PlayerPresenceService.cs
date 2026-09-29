namespace Kahoot.Infrastructure.Realtime;

using System.Collections.Concurrent;
using Kahoot.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

public sealed class PlayerPresenceService : IPlayerPresenceService
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan KeyRetention = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, PlayerConnection> _connections = new();
    private readonly IDatabase _database;
    private readonly ISubscriber _subscriber;
    private readonly string _keyPrefix;
    private readonly string _instanceId = Guid.NewGuid().ToString("N");
    private readonly TimeProvider _timeProvider;

    public PlayerPresenceService(
        IConnectionMultiplexer redis,
        IOptions<RealtimeOptions> options,
        TimeProvider timeProvider)
    {
        _database = redis.GetDatabase();
        _subscriber = redis.GetSubscriber();
        _keyPrefix = options.Value.ChannelPrefix;
        _timeProvider = timeProvider;
    }

    public bool TryGetConnection(string connectionId, out PlayerConnectionInfo info)
    {
        if (_connections.TryGetValue(connectionId, out PlayerConnection? connection))
        {
            info = new PlayerConnectionInfo(
                connection.ParticipantId,
                connection.HostAccountId,
                connection.GameId,
                connection.Nickname,
                connection.SeatNumber,
                connection.ConnectionGeneration);
            return true;
        }

        info = default;
        return false;
    }

    public async Task<bool> HasActiveConnectionAsync(Guid participantId, CancellationToken cancellationToken)
    {
        RedisKey key = ParticipantKey(participantId);
        long now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        await _database.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, now)
            .WaitAsync(cancellationToken);
        return await _database.SortedSetLengthAsync(key).WaitAsync(cancellationToken) > 0;
    }

    public async Task RegisterAsync(
        string connectionId,
        Guid participantId,
        Guid hostAccountId,
        Guid gameId,
        string nickname,
        int seatNumber,
        long connectionGeneration,
        Action abortConnection)
    {
        PlayerConnection connection = new PlayerConnection(
            participantId,
            hostAccountId,
            gameId,
            nickname,
            seatNumber,
            connectionGeneration,
            abortConnection);

        if (!_connections.TryAdd(connectionId, connection))
        {
            throw new InvalidOperationException("A player connection is already attached.");
        }

        await connection.Gate.WaitAsync();
        try
        {
            await RefreshAsync(connectionId, connection);
        }
        catch
        {
            _connections.TryRemove(connectionId, out _);
            await _database.SortedSetRemoveAsync(Key(gameId), Member(connectionId));
            await _database.SortedSetRemoveAsync(ParticipantKey(participantId), Member(connectionId));
            throw;
        }
        finally
        {
            connection.Gate.Release();
        }
    }

    public async Task<bool> RemoveAsync(string connectionId, Guid gameId)
    {
        if (_connections.TryGetValue(connectionId, out PlayerConnection? connection))
        {
            await connection.Gate.WaitAsync();
            try
            {
                if (_connections.TryGetValue(connectionId, out PlayerConnection? current) &&
                    ReferenceEquals(current, connection) && _connections.TryRemove(connectionId, out _))
                {
                    await _database.SortedSetRemoveAsync(Key(gameId), Member(connectionId));
                    await _database.SortedSetRemoveAsync(ParticipantKey(connection.ParticipantId), Member(connectionId));
                }
            }
            finally
            {
                connection.Gate.Release();
            }
        }

        return await HasAnyAsync(gameId);
    }

    public async Task<int> GetConnectedCountAsync(Guid gameId)
    {
        RedisKey key = Key(gameId);
        long now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        await _database.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, now);
        long count = await _database.SortedSetLengthAsync(key);
        return (int)count;
    }

    public async Task<bool> HasAnyAsync(Guid gameId)
    {
        RedisKey key = Key(gameId);
        long now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        await _database.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, now);
        long count = await _database.SortedSetLengthAsync(key);
        return count > 0;
    }

    public void AbortParticipantConnections(Guid participantId)
    {
        foreach (PlayerConnection connection in _connections.Values)
        {
            if (connection.ParticipantId == participantId)
            {
                connection.Abort();
            }
        }
    }

    public void AbortHostConnections(Guid hostAccountId)
    {
        foreach (PlayerConnection connection in _connections.Values)
        {
            if (connection.HostAccountId == hostAccountId)
            {
                connection.Abort();
            }
        }
    }

    public async Task EvictParticipantAsync(Guid participantId, Guid gameId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _subscriber.PublishAsync(
            RedisChannel.Literal($"{_keyPrefix}:player-sockets:evict"),
            $"kick:{gameId:N}:{participantId:N}").WaitAsync(cancellationToken);
    }

    public async Task FenceParticipantAsync(Guid participantId, Guid gameId, long generation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _subscriber.PublishAsync(
            RedisChannel.Literal($"{_keyPrefix}:player-sockets:evict"),
            $"fence:{gameId:N}:{participantId:N}:{generation}").WaitAsync(cancellationToken);
    }

    public string[] GetParticipantConnectionIds(Guid participantId) => _connections
        .Where(entry => entry.Value.ParticipantId == participantId)
        .Select(entry => entry.Key)
        .ToArray();

    public void AbortStaleParticipantConnections(Guid participantId, long generation)
    {
        foreach (PlayerConnection connection in _connections.Values)
        {
            if (connection.ParticipantId == participantId && connection.ConnectionGeneration < generation)
            {
                connection.Abort();
            }
        }
    }

    public async Task RenewAllAsync(CancellationToken cancellationToken)
    {
        foreach (KeyValuePair<string, PlayerConnection> entry in _connections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await entry.Value.Gate.WaitAsync(cancellationToken);
            try
            {
                if (_connections.TryGetValue(entry.Key, out PlayerConnection? current) &&
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

    private async Task RefreshAsync(string connectionId, PlayerConnection connection)
    {
        long expiresAt = _timeProvider.GetUtcNow().Add(LeaseDuration).ToUnixTimeMilliseconds();
        RedisValue member = Member(connectionId);
        RedisKey gameKey = Key(connection.GameId);
        RedisKey participantKey = ParticipantKey(connection.ParticipantId);
        await _database.SortedSetAddAsync(gameKey, member, expiresAt);
        await _database.SortedSetAddAsync(participantKey, member, expiresAt);
        await _database.KeyExpireAsync(gameKey, KeyRetention);
        await _database.KeyExpireAsync(participantKey, KeyRetention);
    }

    private RedisKey Key(Guid gameId) => $"{_keyPrefix}:game:{gameId:N}:players";

    private RedisKey ParticipantKey(Guid participantId) => $"{_keyPrefix}:participant:{participantId:N}:connections";

    private RedisValue Member(string connectionId) => $"{_instanceId}:{connectionId}";

    private sealed class PlayerConnection
    {
        public PlayerConnection(
            Guid participantId,
            Guid hostAccountId,
            Guid gameId,
            string nickname,
            int seatNumber,
            long connectionGeneration,
            Action abort)
        {
            ParticipantId = participantId;
            HostAccountId = hostAccountId;
            GameId = gameId;
            Nickname = nickname;
            SeatNumber = seatNumber;
            ConnectionGeneration = connectionGeneration;
            Abort = abort;
        }

        public Guid ParticipantId { get; }

        public Guid HostAccountId { get; }

        public Guid GameId { get; }

        public string Nickname { get; }

        public int SeatNumber { get; }

        public long ConnectionGeneration { get; }

        public Action Abort { get; }

        public SemaphoreSlim Gate { get; } = new(1, 1);
    }
}

public readonly record struct PlayerConnectionInfo(
    Guid ParticipantId,
    Guid HostAccountId,
    Guid GameId,
    string Nickname,
    int SeatNumber,
    long ConnectionGeneration);
