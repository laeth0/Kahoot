using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Pagination;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Features.Admin.Users.ListUsers;

public sealed class ListUsersQueryHandler : IQueryHandler<ListUsersQuery, ListUsersResponse>
{
    private readonly IAppDbContext _dbContext;

    public ListUsersQueryHandler(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ListUsersResponse>> Handle(
        ListUsersQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<User> queryable = _dbContext.Users
            .AsNoTracking()
            .TagWith("Admin:ListUsers")
            .Where(user => user.Role == UserRole.Host);

        if (query.Status.HasValue)
        {
            queryable = queryable.Where(user => user.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Username))
        {
            string displayUsername = UsernameNormalization.GetDisplayUsername(query.Username);
            string normalizedPrefix = UsernameNormalization.GetNormalizedUsername(displayUsername);
            queryable = queryable.Where(user => user.NormalizedUsername.StartsWith(normalizedPrefix));
        }

        if (!string.IsNullOrWhiteSpace(query.Cursor) &&
            KeysetCursor.TryDecode(query.Cursor, out KeysetCursor? cursor) &&
            cursor is not null)
        {
            queryable = queryable.Where(user =>
                user.CreatedAt < cursor.CreatedAt ||
                (user.CreatedAt == cursor.CreatedAt && user.Id < cursor.Id));
        }

        queryable = queryable
            .OrderByDescending(user => user.CreatedAt)
            .ThenByDescending(user => user.Id);

        int fetchLimit = query.PageSize + 1;

        List<UserAdminItemResponse> items = await queryable
            .Take(fetchLimit)
            .Select(user => new UserAdminItemResponse(
                user.Id,
                user.DisplayUsername,
                user.Role,
                user.Status,
                user.CreatedAt,
                user.Status == UserStatus.Suspended ? (DateTimeOffset?)user.UpdatedAt : null,
                user.Revision,
                user.TerminationPending))
            .ToListAsync(cancellationToken);

        bool hasMore = items.Count > query.PageSize;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        string? nextCursor = hasMore && items.Count > 0
            ? KeysetCursor.Encode(items[^1].CreatedAt, items[^1].AccountId)
            : null;

        ListUsersResponse response = new ListUsersResponse(
            items,
            nextCursor,
            hasMore);

        return Result.Success(response);
    }
}
