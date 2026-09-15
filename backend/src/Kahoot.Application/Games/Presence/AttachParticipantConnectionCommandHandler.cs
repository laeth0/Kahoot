using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Games.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.Presence;

internal sealed class AttachParticipantConnectionCommandHandler(
    IApplicationDbContext dbContext,
    TimeProvider timeProvider) : ICommandHandler<AttachParticipantConnectionCommand, ParticipantPresenceMutationResponse>
{
    public async Task<Result<ParticipantPresenceMutationResponse>> Handle(
        AttachParticipantConnectionCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        GameSession? game = await dbContext.GameSessions
            .FromSqlInterpolated($"SELECT * FROM game_sessions WHERE id = {command.GameId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        if (game is null)
        {
            return Result.Failure<ParticipantPresenceMutationResponse>(GameErrors.NotFound);
        }

        Participant? participant = await dbContext.Participants
            .FirstOrDefaultAsync(
                candidate => candidate.Id == command.ParticipantId
                    && candidate.GameSessionId == command.GameId
                    && !candidate.IsRemoved,
                cancellationToken);

        if (participant is null)
        {
            return Result.Failure<ParticipantPresenceMutationResponse>(GameErrors.ParticipantRemoved);
        }

        bool changed = participant.ConnectionId != command.ConnectionId;
        if (changed)
        {
            participant.ConnectionId = command.ConnectionId;
            participant.LastSeenAt = timeProvider.GetUtcNow().UtcDateTime;
            game.PresenceVersion++;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

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
            command.Reason,
            new GameParticipantResponse(
                participant.Id,
                participant.Nickname,
                participant.TotalScore,
                participant.LastRank,
                true,
                false));

        return Result.Success(new ParticipantPresenceMutationResponse(presence, changed));
    }
}
