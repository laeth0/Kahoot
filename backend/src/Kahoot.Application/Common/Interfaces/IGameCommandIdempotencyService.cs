namespace Kahoot.Application.Common.Interfaces;

using Kahoot.Application.Common.Results;

public sealed record IdempotencyCheckResult<TResponse>(
    bool IsReplay,
    TResponse? CachedResponse,
    Error? Error);

public interface IGameCommandIdempotencyService
{
    Task<IdempotencyCheckResult<TResponse>> CheckAsync<TResponse>(
        Guid gameId,
        Guid commandId,
        string commandName,
        object requestPayload,
        CancellationToken cancellationToken);

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
