namespace Kahoot.Application.Common.Interfaces;

public interface IGameNotificationService
{
    // Question Started Broadcast (RT-ORD-001) - Emits separate player (choice text only) and host payloads after transaction commit
    Task PublishQuestionStartedAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object playerPayload,
        object hostPayload,
        CancellationToken cancellationToken = default);

    // Question Ended Broadcast - Emits question results and answer distribution statistics
    Task PublishQuestionEndedAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object payload,
        CancellationToken cancellationToken = default);

    // Leaderboard Updated Broadcast - Emits top podium ranks and updated cumulative scores
    Task PublishLeaderboardUpdatedAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object payload,
        CancellationToken cancellationToken = default);

    // Game Ended Broadcast - Emits final game summary and releases lobby PIN discovery
    Task PublishGameEndedAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object payload,
        CancellationToken cancellationToken = default);

    // Participant Presence Broadcast - Emits join/leave/ejection events to update lobby participant list
    Task PublishParticipantPresenceChangedAsync(
        Guid hostAccountId,
        Guid gameId,
        long presenceVersion,
        object payload,
        CancellationToken cancellationToken = default);

}
