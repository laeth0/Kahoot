namespace Kahoot.Infrastructure.Security;

using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Infrastructure.Realtime;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

public sealed class LobbyJoinRateLimiter : ILobbyJoinRateLimiter
{
    private const string ConsumeScript = """
        local time = redis.call('TIME')
        local now = tonumber(time[1]) * 1000 + math.floor(tonumber(time[2]) / 1000)
        local state = redis.call('HMGET', KEYS[1], 'tokens', 'updated')
        local tokens = tonumber(state[1]) or 1200
        local updated = tonumber(state[2]) or now
        tokens = math.min(1200, tokens + math.max(0, now - updated) * 0.06)
        local accepted = 0
        if tokens >= 1 then
            tokens = tokens - 1
            accepted = 1
        end
        redis.call('HSET', KEYS[1], 'tokens', tokens, 'updated', now)
        redis.call('PEXPIRE', KEYS[1], 30000)
        return accepted
        """;

    private readonly IDatabase _database;
    private readonly string _keyPrefix;

    public LobbyJoinRateLimiter(IConnectionMultiplexer redis, IOptions<RealtimeOptions> options)
    {
        _database = redis.GetDatabase();
        _keyPrefix = options.Value.ChannelPrefix;
    }

    public async Task<bool> IsRateLimitedAsync(string ipAddress, CancellationToken cancellationToken)
    {
        string clientAddress = string.IsNullOrWhiteSpace(ipAddress) ? "unknown" : ipAddress;
        string addressHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(clientAddress)));
        RedisKey key = $"{_keyPrefix}:lobby-join-rate:{addressHash}";
        RedisResult accepted = await _database.ScriptEvaluateAsync(
            ConsumeScript, [key], []).WaitAsync(cancellationToken);
        return (long)accepted == 0;
    }
}
