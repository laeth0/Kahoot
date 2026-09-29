namespace Kahoot.Infrastructure.Services;

using System.Security.Cryptography;
using System.Text.Json;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Features.Games;
using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// Game Command Idempotency Filter - Deduplicates incoming host state-mutating commands using client-supplied command UUID and SHA-256 payload digest.
public sealed class GameCommandIdempotencyService : IGameCommandIdempotencyService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IAppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GameCommandIdempotencyService> _logger;

    public GameCommandIdempotencyService(
        IAppDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<GameCommandIdempotencyService> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // Idempotency Check - Verifies if command has executed previously and returns cached response or rejects payload mutations.
    public async Task<IdempotencyCheckResult<TResponse>> CheckAsync<TResponse>(
        Guid gameId,
        Guid commandId,
        string commandName,
        object requestPayload,
        CancellationToken cancellationToken)
    {
        // Cryptographic Payload Digest - Computes SHA-256 hash of JSON-serialized command arguments.
        byte[] requestBytes = JsonSerializer.SerializeToUtf8Bytes(requestPayload, JsonOptions);
        byte[] requestHash = SHA256.HashData(requestBytes);

        GameCommandIdempotency? existingRecord = await _dbContext.GameCommandIdempotencies
            .AsNoTracking()
            .FirstOrDefaultAsync(
                cmd => cmd.GameId == gameId && cmd.CommandId == commandId,
                cancellationToken);

        if (existingRecord is null)
        {
            return new IdempotencyCheckResult<TResponse>(false, default, null);
        }

        // Constant-Time Hash Comparison - Prevents timing analysis while verifying payload fidelity on replayed command.
        bool hashesMatch = CryptographicOperations.FixedTimeEquals(existingRecord.RequestHash, requestHash);
        if (!hashesMatch || !string.Equals(existingRecord.CommandName, commandName, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Idempotent command replayed with altered request parameters. EventName={EventName} GameId={GameId} CommandId={CommandId}",
                "IdempotentCommandPayloadMismatch",
                gameId,
                commandId);

            return new IdempotencyCheckResult<TResponse>(true, default, GameErrors.ValidationFailed);
        }

        TResponse? cachedResponse = JsonSerializer.Deserialize<TResponse>(existingRecord.ResponsePayload, JsonOptions);

        _logger.LogInformation(
            "Idempotent command replayed successfully from cache. EventName={EventName} GameId={GameId} CommandId={CommandId} ResultStateVersion={ResultStateVersion}",
            "IdempotentCommandReplayed",
            gameId,
            commandId,
            existingRecord.ResultStateVersion);

        return new IdempotencyCheckResult<TResponse>(true, cachedResponse, null);
    }

    // Idempotency Record Persistence - Stores executed command payload hash, resulting state version, and response JSON for replay.
    public Task RecordAsync<TResponse>(
        Guid gameId,
        Guid hostAccountId,
        Guid commandId,
        string commandName,
        object requestPayload,
        long resultStateVersion,
        TResponse response,
        CancellationToken cancellationToken)
    {
        byte[] requestBytes = JsonSerializer.SerializeToUtf8Bytes(requestPayload, JsonOptions);
        byte[] requestHash = SHA256.HashData(requestBytes);
        string responseJson = JsonSerializer.Serialize(response, JsonOptions);

        GameCommandIdempotency record = new GameCommandIdempotency
        {
            GameId = gameId,
            HostAccountId = hostAccountId,
            CommandId = commandId,
            CommandName = commandName,
            RequestHash = requestHash,
            ResultStateVersion = resultStateVersion,
            ResponsePayload = responseJson,
            CreatedAt = _timeProvider.GetUtcNow()
        };

        _dbContext.GameCommandIdempotencies.Add(record);
        return Task.CompletedTask;
    }
}
