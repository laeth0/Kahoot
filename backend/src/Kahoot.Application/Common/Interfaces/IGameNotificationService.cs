namespace Kahoot.Application.Common.Interfaces;

using Kahoot.Application.Features.Games.Models;

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

    // Dual-Audience Question Results Broadcast (SCORE-RES-002) - Emits aggregate distributions to host/players and personal score cards to individual participants
    Task PublishQuestionEndedWithPersonalResultsAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object aggregatePayload,
        IReadOnlyList<PersonalQuestionResultEvent> personalResults,
        CancellationToken cancellationToken = default);

    // Leaderboard Updated Broadcast - Emits top podium ranks and updated cumulative scores
    Task PublishLeaderboardUpdatedAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object payload,
        CancellationToken cancellationToken = default);

    // Dual-Audience Leaderboard Broadcast (GAME-CTRL-003) - Emits top podium to host/players and individual sequential ranks to each player
    Task PublishLeaderboardUpdatedWithPersonalRanksAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object aggregatePayload,
        IReadOnlyList<PersonalLeaderboardEvent> personalRanks,
        CancellationToken cancellationToken = default);

    // Game Ended Broadcast - Emits final game summary and releases lobby PIN discovery
    Task PublishGameEndedAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object payload,
        CancellationToken cancellationToken = default);

    // Dual-Audience Final Podium Broadcast (SCORE-RANK-001) - Emits top finalists to host/players and individual final ranks to each player
    Task PublishGameEndedWithPersonalRanksAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object aggregatePayload,
        IReadOnlyList<PersonalGameEndedEvent> personalRanks,
        CancellationToken cancellationToken = default);

    // Participant Presence Broadcast - Emits join/leave/ejection events to update lobby participant list
    Task PublishParticipantPresenceChangedAsync(
        Guid hostAccountId,
        Guid gameId,
        long presenceVersion,
        object payload,
        CancellationToken cancellationToken = default);
}
