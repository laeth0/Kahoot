namespace Kahoot.Application.Features.Games.RemoveParticipant;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Games.Models;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

public sealed class RemoveParticipantCommandHandler : ICommandHandler<RemoveParticipantCommand>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IPlayerPresenceService _playerPresenceService;
    private readonly IGameNotificationService _notificationService;
    private readonly ILogger<RemoveParticipantCommandHandler> _logger;

    public RemoveParticipantCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        IPlayerPresenceService playerPresenceService,
        IGameNotificationService notificationService,
        ILogger<RemoveParticipantCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _playerPresenceService = playerPresenceService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Result> Handle(
        RemoveParticipantCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        // Transactional Atomicity: Wraps participant removal, token revocation, seat count update, and auto-close check in one transaction
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Concurrency & Pessimistic Row Lock: GetUserForUpdateAsync and GetGameForUpdateAsync serialize host control actions
        User? host = await _dbContext.GetUserForUpdateAsync(hostAccountId, cancellationToken);
        if (host is null || host.Role != UserRole.Host || host.Status != UserStatus.Active)
        {
            return Result.Failure(AuthErrors.Unauthorized);
        }

        Game? game = await _dbContext.GetGameForUpdateAsync(request.GameId, hostAccountId, cancellationToken);
        if (game is null)
        {
            return Result.Failure(GameErrors.NotFound);
        }

        if (game.Status == GameStatus.Finished)
        {
            return Result.Failure(GameErrors.InvalidStateTransition);
        }

        // Query Performance: FirstOrDefaultAsync seeks participant record by ID within game and host tenant boundaries
        Participant? participant = await _dbContext.Participants
            .FirstOrDefaultAsync(
                p => p.Id == request.ParticipantId && p.GameId == game.Id && p.HostAccountId == hostAccountId,
                cancellationToken);

        if (participant is null)
        {
            return Result.Failure(GameErrors.ParticipantNotFound);
        }

        // Idempotent Eviction: If already removed, commit and re-evict socket safely
        if (participant.IsRemoved)
        {
            await transaction.CommitAsync(cancellationToken);
            await _playerPresenceService.EvictParticipantAsync(
                participant.Id, game.Id, CancellationToken.None);
            return Result.Success();
        }

        DateTimeOffset utcNow = await _dbContext.Database
            .SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
        // System Design & Tombstone Reservation: Soft-deletes participant to permanently reserve nickname and prevent spoofing
        participant.IsRemoved = true;
        participant.RemovedAt = utcNow;

        // Query Performance: Retrieves active participant session tokens for batch revocation
        List<ParticipantSessionToken> tokens = await _dbContext.ParticipantSessionTokens
            .Where(t => t.ParticipantId == participant.Id && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (ParticipantSessionToken token in tokens)
        {
            token.RevokedAt = utcNow;
        }

        game.PresenceVersion += 1;

        GameQuestionSnapshot? activeQuestion = null;
        QuestionEndedEvent? endedEvent = null;

        if (game.Status == GameStatus.Lobby)
        {
            game.ReservedParticipantCount = Math.Max(0, game.ReservedParticipantCount - 1);
        }
        else if (game.Status == GameStatus.QuestionActive && game.CurrentQuestionIndex.HasValue)
        {
            activeQuestion = await _dbContext.GameQuestionSnapshots
                .FirstOrDefaultAsync(
                    q => q.GameId == game.Id && q.HostAccountId == hostAccountId && q.OrderIndex == game.CurrentQuestionIndex.Value,
                    cancellationToken);

            if (activeQuestion is not null)
            {
                // Dynamic Auto-Close Evaluation (GAME-AUTO-002): Checks if removed participant had answered; decrements count and triggers auto-close if all remaining answered
                bool hasAnswered = await _dbContext.AnswerSubmissions
                    .AnyAsync(
                        a => a.GameQuestionId == activeQuestion.Id && a.ParticipantId == participant.Id,
                        cancellationToken);

                if (!hasAnswered)
                {
                    activeQuestion.EffectiveEligibleParticipantCount = Math.Max(0, activeQuestion.EffectiveEligibleParticipantCount - 1);

                    if (activeQuestion.AcceptedAnswerCount == activeQuestion.EffectiveEligibleParticipantCount &&
                        activeQuestion.EffectiveEligibleParticipantCount > 0)
                    {
                        (GameQuestionSnapshot materializedQuestion, List<GameChoiceSnapshot> choices) =
                            await QuestionResultsMaterializer.MaterializeAsync(
                                _dbContext, game, utcNow, cancellationToken);
                        game.Status = GameStatus.QuestionResults;
                        game.StateVersion++;
                        List<QuestionChoiceResultDto> choiceResults = choices
                            .Select(choice => new QuestionChoiceResultDto(
                                choice.Id, choice.OrderIndex, choice.Text, choice.IsCorrect, choice.SelectionCount))
                            .ToList();
                        endedEvent = new QuestionEndedEvent(
                            game.Id, game.StateVersion, activeQuestion.Id, activeQuestion.OrderIndex,
                            materializedQuestion.AcceptedAnswerCount,
                            materializedQuestion.EffectiveEligibleParticipantCount, utcNow,
                            choiceResults.Where(choice => choice.IsCorrect)
                                .Select(choice => choice.ChoiceId).ToList(), choiceResults);
                    }
                }
            }
        }

        // Persistence: Commits participant removal and token revocation atomically
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Participant removed by Host. EventName={EventName} GameId={GameId} ParticipantId={ParticipantId} SeatNumber={SeatNumber}",
            "ParticipantRemovedByHost",
            game.Id,
            participant.Id,
            participant.SeatNumber);

        // Post-Commit Cluster-Wide Socket Eviction: Disconnects removed player's SignalR connection across all cluster nodes
        await _playerPresenceService.EvictParticipantAsync(
            participant.Id,
            game.Id,
            CancellationToken.None);

        int connectedCount = await _playerPresenceService.GetConnectedCountAsync(game.Id);
        ParticipantPresenceChangedEvent presenceEvent = new ParticipantPresenceChangedEvent(
            game.Id,
            game.PresenceVersion,
            game.ReservedParticipantCount,
            connectedCount,
            participant.DisplayNickname,
            participant.SeatNumber,
            "Removed");

        // Post-Commit Broadcast Pattern: Fans out presence update to host socket
        await _notificationService.PublishParticipantPresenceChangedAsync(
            hostAccountId,
            game.Id,
            game.PresenceVersion,
            presenceEvent,
            CancellationToken.None);

        if (endedEvent is not null)
        {
            await _notificationService.PublishQuestionEndedAsync(
                hostAccountId, game.Id, game.StateVersion, endedEvent, CancellationToken.None);
        }

        return Result.Success();
    }
}
