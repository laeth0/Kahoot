namespace Kahoot.Application.Common.Interfaces;

using Kahoot.Application.Common.Results;

public sealed record IdempotencyCheckResult<TResponse>(
    bool IsReplay,
    TResponse? CachedResponse,
    Error? Error);

public interface IGameCommandIdempotencyService
{
    // Idempotent Check (GAME-IDEMP-001) - Verifies if CommandId has been executed; returns cached response or detects payload conflicts
    Task<IdempotencyCheckResult<TResponse>> CheckAsync<TResponse>(
        Guid gameId,
        Guid commandId,
        string commandName,
        object requestPayload,
        CancellationToken cancellationToken);

    // Idempotent Execution Persistence (GAME-IDEMP-002) - Caches successful state version and serialized response for future replaying
    Task RecordAsync<TResponse>(
        Guid gameId,
        Guid hostAccountId,
        Guid commandId,
        string commandName,
        object requestPayload,
        long resultStateVersion,
        TResponse response,
        CancellationToken cancellationToken);
}
