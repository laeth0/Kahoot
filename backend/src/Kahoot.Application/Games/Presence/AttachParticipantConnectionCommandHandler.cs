using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
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

        await dbContext.Participants
            .Where(participant => participant.Id == command.ParticipantId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(participant => participant.ConnectionId, command.ConnectionId)
                    .SetProperty(participant => participant.LastSeenAt, now),
                cancellationToken);

        return Result.Success();
    }
}
