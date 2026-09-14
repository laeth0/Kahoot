using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Games.Common;
using Kahoot.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.Presence;

internal sealed class AttachParticipantConnectionCommandHandler(
    IApplicationDbContext dbContext,
    TimeProvider timeProvider) : ICommandHandler<AttachParticipantConnectionCommand>
{
    public async Task<Result> Handle(AttachParticipantConnectionCommand command, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        int updated = await dbContext.Participants
            .Where(participant => participant.Id == command.ParticipantId && !participant.IsRemoved)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(participant => participant.ConnectionId, command.ConnectionId)
                    .SetProperty(participant => participant.LastSeenAt, now),
                cancellationToken);

        return updated > 0
            ? Result.Success()
            : Result.Failure(GameErrors.ParticipantRemoved);
    }
}
