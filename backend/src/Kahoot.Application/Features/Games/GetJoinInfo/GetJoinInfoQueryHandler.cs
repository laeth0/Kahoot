namespace Kahoot.Application.Features.Games.GetJoinInfo;

using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

public sealed class GetJoinInfoQueryHandler : IQueryHandler<GetJoinInfoQuery, GetJoinInfoResponse>
{
    private readonly IAppDbContext _dbContext;

    public GetJoinInfoQueryHandler(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<GetJoinInfoResponse>> Handle(
        GetJoinInfoQuery request,
        CancellationToken cancellationToken)
    {
        Game? game = await _dbContext.Games
            .AsNoTracking()
            .FirstOrDefaultAsync(
                g => g.Pin == request.Pin && g.Status != GameStatus.Finished,
                cancellationToken);

        if (game is null)
        {
            return Result.Failure<GetJoinInfoResponse>(GameErrors.InvalidPin);
        }

        if (game.Status != GameStatus.Lobby)
        {
            return Result.Failure<GetJoinInfoResponse>(GameErrors.NotJoinable);
        }

        bool hostIsActive = await _dbContext.Users.AsNoTracking().AnyAsync(
            user => user.Id == game.HostAccountId && user.Role == UserRole.Host &&
                    user.Status == UserStatus.Active,
            cancellationToken);
        if (!hostIsActive)
        {
            return Result.Failure<GetJoinInfoResponse>(GameErrors.InvalidPin);
        }

        int activeParticipantCount = await _dbContext.Participants
            .AsNoTracking()
            .CountAsync(
                p => p.GameId == game.Id && !p.IsRemoved,
                cancellationToken);

        const int maxCapacity = 500;
        bool isFull = activeParticipantCount >= maxCapacity;

        GetJoinInfoResponse response = new GetJoinInfoResponse(
            game.Id,
            game.Title,
            "LOBBY",
            activeParticipantCount,
            maxCapacity,
            isFull);

        return Result.Success(response);
    }
}
