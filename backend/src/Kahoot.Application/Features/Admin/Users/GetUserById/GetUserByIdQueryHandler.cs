using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Features.Admin.Users.GetUserById;

public sealed class GetUserByIdQueryHandler : IQueryHandler<GetUserByIdQuery, UserAdminResponse>
{
    private readonly IAppDbContext _dbContext;

    public GetUserByIdQueryHandler(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<UserAdminResponse>> Handle(
        GetUserByIdQuery query,
        CancellationToken cancellationToken)
    {
        UserAdminResponse? user = await _dbContext.Users
            .AsNoTracking()
            .TagWith("Admin:GetUserById")
            .Where(u => u.Id == query.AccountId && u.Role == UserRole.Host)
            .Select(u => new UserAdminResponse(
                u.Id,
                u.DisplayUsername,
                u.Role,
                u.Status,
                u.CreatedAt,
                u.Status == UserStatus.Suspended ? (DateTimeOffset?)u.UpdatedAt : null,
                u.Revision,
                u.TerminationPending))
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Result.Failure<UserAdminResponse>(AccountErrors.NotFound);
        }

        return Result.Success(user);
    }
}
