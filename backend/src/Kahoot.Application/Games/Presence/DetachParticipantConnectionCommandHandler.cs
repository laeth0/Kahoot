using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.Presence;

internal sealed class DetachParticipantConnectionCommandHandler(
    IApplicationDbContext dbContext,
    TimeProvider timeProvider) : ICommandHandler<DetachParticipantConnectionCommand, bool>
{
    public async Task<Result<bool>> Handle(DetachParticipantConnectionCommand command, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        int rowsAffected = await dbContext.Participants
            .Where(participant =>
                participant.Id == command.ParticipantId &&
                participant.ConnectionId == command.ConnectionId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(participant => participant.ConnectionId, (string?)null)
                    .SetProperty(participant => participant.LastSeenAt, now),
                cancellationToken);

        return Result.Success(rowsAffected > 0);
    }
}
