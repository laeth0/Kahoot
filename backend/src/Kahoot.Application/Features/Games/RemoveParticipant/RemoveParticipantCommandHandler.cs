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
    private readonly IGameAutoCloseService _autoCloseService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RemoveParticipantCommandHandler> _logger;

    public RemoveParticipantCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        IPlayerPresenceService playerPresenceService,
        IGameNotificationService notificationService,
        IGameAutoCloseService autoCloseService,
        TimeProvider timeProvider,
        ILogger<RemoveParticipantCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _playerPresenceService = playerPresenceService;
        _notificationService = notificationService;
        _autoCloseService = autoCloseService;
        _timeProvider = timeProvider;
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

        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

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

        Participant? participant = await _dbContext.Participants
            .FirstOrDefaultAsync(
                p => p.Id == request.ParticipantId && p.GameId == game.Id && p.HostAccountId == hostAccountId,
                cancellationToken);

        if (participant is null)
        {
            return Result.Failure(GameErrors.ParticipantNotFound);
        }

        if (participant.IsRemoved)
        {
            await transaction.CommitAsync(cancellationToken);
            await _playerPresenceService.EvictParticipantAsync(
                participant.Id, game.Id, CancellationToken.None);
            return Result.Success();
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        participant.IsRemoved = true;
        participant.RemovedAt = utcNow;

        List<ParticipantSessionToken> tokens = await _dbContext.ParticipantSessionTokens
            .Where(t => t.ParticipantId == participant.Id && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (ParticipantSessionToken token in tokens)
        {
            token.RevokedAt = utcNow;
        }

        game.PresenceVersion += 1;

        GameQuestionSnapshot? activeQuestion = null;
        bool shouldEvaluateAutoClose = false;

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
                        shouldEvaluateAutoClose = true;
                    }
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Participant removed by Host. EventName={EventName} GameId={GameId} ParticipantId={ParticipantId} SeatNumber={SeatNumber}",
            "ParticipantRemovedByHost",
            game.Id,
            participant.Id,
            participant.SeatNumber);

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

        await _notificationService.PublishParticipantPresenceChangedAsync(
            hostAccountId,
            game.Id,
            game.PresenceVersion,
            presenceEvent,
            CancellationToken.None);

        if (shouldEvaluateAutoClose)
        {
            try
            {
                await _autoCloseService.TryAutoCloseQuestionAsync(game.Id, CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "Failed to trigger auto-close after participant removal. GameId={GameId}",
                    game.Id);
            }
        }

        return Result.Success();
    }
}
