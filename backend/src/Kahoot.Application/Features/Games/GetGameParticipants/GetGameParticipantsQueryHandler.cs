namespace Kahoot.Application.Features.Games.GetGameParticipants;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

public sealed class GetGameParticipantsQueryHandler : IQueryHandler<GetGameParticipantsQuery, GetGameParticipantsResponse>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetGameParticipantsQueryHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<Result<GetGameParticipantsResponse>> Handle(
        GetGameParticipantsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<GetGameParticipantsResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        Game? game = await _dbContext.Games
            .AsNoTracking()
            .FirstOrDefaultAsync(
                g => g.Id == request.GameId && g.HostAccountId == hostAccountId,
                cancellationToken);

        if (game is null)
        {
            return Result.Failure<GetGameParticipantsResponse>(GameErrors.NotFound);
        }

        IQueryable<Participant> query = _dbContext.Participants
            .AsNoTracking()
            .Where(p => p.GameId == game.Id && p.HostAccountId == hostAccountId);

        if (!request.IncludeRemoved)
        {
            query = query.Where(p => !p.IsRemoved);
        }

        if (request.Cursor.HasValue)
        {
            query = query.Where(p => p.SeatNumber > request.Cursor.Value);
        }

        int pageSize = request.Limit ?? 100;
        List<Participant> participants = await query
            .OrderBy(p => p.SeatNumber)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        int? nextCursor = null;
        if (participants.Count > pageSize)
        {
            Participant lastParticipant = participants[pageSize - 1];
            nextCursor = lastParticipant.SeatNumber;
            participants.RemoveAt(pageSize);
        }

        List<ParticipantDto> participantDtos = participants
            .Select(p => new ParticipantDto(
                p.Id,
                p.DisplayNickname,
                p.SeatNumber,
                p.IsRemoved,
                p.CreatedAt))
            .ToList();

        GetGameParticipantsResponse response = new GetGameParticipantsResponse(
            game.Id,
            game.PresenceVersion,
            game.ReservedParticipantCount,
            participantDtos,
            nextCursor);

        return Result.Success(response);
    }
}
