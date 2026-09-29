namespace Kahoot.Infrastructure.Security;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Infrastructure.Realtime;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

// Multi-Tier Answer Rate Limiter - Enforces per-connection bursts and participant-scoped attempt limits via atomic Redis scripts.
public sealed class AnswerRateLimiter : IAnswerRateLimiter
{
    // Redis TIME keeps the rolling socket window consistent across application replicas.
    private const string RateLimitScript = """
        local socket_limited = false
        if ARGV[1] == '1' then
            local now = redis.call('TIME')
            local now_ms = now[1] * 1000 + math.floor(now[2] / 1000)
            redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', now_ms - 3000)
            if redis.call('ZCARD', KEYS[1]) >= 5 then
                socket_limited = true
            else
                local sequence = redis.call('INCR', KEYS[2])
                redis.call('PEXPIRE', KEYS[2], 3000)
                redis.call('ZADD', KEYS[1], now_ms, now_ms .. ':' .. sequence)
                redis.call('PEXPIRE', KEYS[1], 3000)
            end
        end

        local part_attempts = redis.call('INCR', KEYS[3])
        if part_attempts == 1 then
            redis.call('EXPIRE', KEYS[3], 600)
        end
        if socket_limited or part_attempts > 10 then
            return 2
        end

        return 0
        """;

    private readonly IDatabase _database;
    private readonly string _keyPrefix;

    public AnswerRateLimiter(IConnectionMultiplexer redis, IOptions<RealtimeOptions> options)
    {
        _database = redis.GetDatabase();
        _keyPrefix = options.Value.ChannelPrefix;
    }

    // Rate Limit Evaluation - Evaluates connection and participant attempt keys in Redis to defend against spam and reconnection bypasses.
    public async Task<bool> IsRateLimitedAsync(
        string connectionId,
        Guid gameId,
        Guid participantId,
        Guid questionId,
        CancellationToken cancellationToken)
    {
        bool hasConnection = !string.IsNullOrWhiteSpace(connectionId) && !string.Equals(connectionId, "unknown", StringComparison.OrdinalIgnoreCase);
        string connKeyString = hasConnection ? connectionId : "none";

        RedisKey connKey = $"{_keyPrefix}:rate:ans:{{{gameId:N}}}:conn:{connKeyString}";
        RedisKey sequenceKey = $"{_keyPrefix}:rate:ans:{{{gameId:N}}}:seq:{connKeyString}";
        RedisKey partKey = $"{_keyPrefix}:rate:ans:{{{gameId:N}}}:part:{participantId:N}:{questionId:N}";

        RedisKey[] keys = [connKey, sequenceKey, partKey];
        RedisValue[] values = [hasConnection ? "1" : "0"];

        // Atomic Script Execution - Runs multi-key Lua script with TTL bounds to prevent memory bloat in Redis.
        RedisResult result = await _database.ScriptEvaluateAsync(RateLimitScript, keys, values).WaitAsync(cancellationToken);
        long code = (long)result;

        return code > 0;
    }
}
