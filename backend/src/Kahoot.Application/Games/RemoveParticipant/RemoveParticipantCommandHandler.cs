using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Games.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.RemoveParticipant;

internal sealed class RemoveParticipantCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<RemoveParticipantCommand, RemoveParticipantResponse>
{
    public async Task<Result<RemoveParticipantResponse>> Handle(RemoveParticipantCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.HostId is not { } hostId)
        {
            return Result.Failure<RemoveParticipantResponse>(SharedErrors.Unauthorized);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        GameSession? game = await dbContext.GameSessions
            .FirstOrDefaultAsync(session => session.Id == command.GameId && session.HostId == hostId, cancellationToken);

        if (game is null)
        {
            return Result.Failure<RemoveParticipantResponse>(GameErrors.NotFound);
        }

        Participant? participant = await dbContext.Participants
            .FirstOrDefaultAsync(
                candidate => candidate.Id == command.ParticipantId && candidate.GameSessionId == command.GameId,
                cancellationToken);
        if (participant is null)
        {
            return Result.Failure<RemoveParticipantResponse>(GameErrors.ParticipantNotFound);
        }

        if (participant.IsRemoved)
        {
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(new RemoveParticipantResponse(null, null));
        }

        string? connectionId = participant.ConnectionId;
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        participant.IsRemoved = true;
        participant.RemovedAt = now;
        participant.ConnectionId = null;
        participant.LastSeenAt = now;
        game.PresenceVersion++;

        await dbContext.SaveChangesAsync(cancellationToken);

        int participantCount = await dbContext.Participants
            .CountAsync(
                candidate => candidate.GameSessionId == command.GameId
                    && candidate.ConnectionId != null
                    && !candidate.IsRemoved,
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var presence = new ParticipantPresenceResponse(
            participantCount,
            game.PresenceVersion,
            ParticipantPresenceReasons.Removed,
            new GameParticipantResponse(
                participant.Id,
                participant.Nickname,
                participant.TotalScore,
                participant.LastRank,
                false,
                true));

        return Result.Success(new RemoveParticipantResponse(connectionId, presence));
    }
}
