namespace Kahoot.Application.Common.Interfaces;

public interface IGameNotificationService
{
    Task PublishQuestionStartedAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object playerPayload,
        object hostPayload,
        CancellationToken cancellationToken = default);

    Task PublishQuestionEndedAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object payload,
        CancellationToken cancellationToken = default);

    Task PublishLeaderboardUpdatedAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object payload,
        CancellationToken cancellationToken = default);

    Task PublishGameEndedAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object payload,
        CancellationToken cancellationToken = default);
}
