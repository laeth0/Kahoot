using Kahoot.Application.Common.Abstractions;
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
    TimeProvider timeProvider) : ICommandHandler<RemoveParticipantCommand>
{
    public async Task<Result> Handle(RemoveParticipantCommand command, CancellationToken cancellationToken)
    {
        Result<GameSession> gameResult = await HostGameGuard.LoadOwnedGameAsync(
            dbContext, currentUser, command.GameId, cancellationToken);
        if (gameResult.IsFailure)
        {
            return Result.Failure(gameResult.Error);
        }

        Participant? participant = await dbContext.Participants
            .FirstOrDefaultAsync(
                candidate => candidate.Id == command.ParticipantId && candidate.GameSessionId == command.GameId,
                cancellationToken);
        if (participant is null)
        {
            return Result.Failure(GameErrors.ParticipantNotFound);
        }

        if (participant.IsRemoved)
        {
            return Result.Success();
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        participant.IsRemoved = true;
        participant.RemovedAt = now;
        participant.ConnectionId = null;
        participant.LastSeenAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
