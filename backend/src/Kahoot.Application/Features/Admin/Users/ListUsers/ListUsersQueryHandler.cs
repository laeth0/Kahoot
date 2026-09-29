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
        // Query Optimization: AsNoTracking - Disables EF change tracker for high-throughput pagination
        // Observability Tagging - Instruments SQL query with TagWith for APM distributed tracing
        // Multi-Tenant Isolation - Scopes administrative enumeration strictly to Host accounts
        IQueryable<User> queryable = _dbContext.Users
            .AsNoTracking()
            .TagWith("Admin:ListUsers")
            .Where(user => user.Role == UserRole.Host);

        // Lifecycle State Filter - Optional filtering by Active or Suspended accounts
        if (query.Status.HasValue)
        {
            queryable = queryable.Where(user => user.Status == query.Status.Value);
        }

        // B-Tree Prefix Search (ACCT-SLO-003) - Normalizes input to Form KC prefix to leverage index on normalized_username with sub-150ms p95
        if (!string.IsNullOrWhiteSpace(query.Username))
        {
            string displayUsername = UsernameNormalization.GetDisplayUsername(query.Username);
            string normalizedPrefix = UsernameNormalization.GetNormalizedUsername(displayUsername);
            queryable = queryable.Where(user => user.NormalizedUsername.StartsWith(normalizedPrefix));
        }

        // Keyset Cursor Seek (ACCT-QUERY-001) - Decodes opaque cursor and seeks via compound condition (CreatedAt, Id) avoiding O(N) OFFSET scan
        if (!string.IsNullOrWhiteSpace(query.Cursor) &&
            KeysetCursor.TryDecode(query.Cursor, out KeysetCursor? cursor) &&
            cursor is not null)
        {
            queryable = queryable.Where(user =>
                user.CreatedAt < cursor.CreatedAt ||
                (user.CreatedAt == cursor.CreatedAt && user.Id < cursor.Id));
        }

        // Deterministic Composite Sorting - Guarantees stable pagination order on (created_at DESC, id DESC) matching index
        queryable = queryable
            .OrderByDescending(user => user.CreatedAt)
            .ThenByDescending(user => user.Id);

        // Bounded Page Over-Fetching (Limit + 1) - Reads one extra record to detect next page existence without separate COUNT(*) query
        int fetchLimit = query.PageSize + 1;

        // Metadata Projection & Privacy Barrier (ACCT-QUERY-002) - Projects strictly administrative metadata, excluding private quiz/game data
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

        // Next Page Cursor Extraction - Encodes opaque cursor from last item of requested window and trims extra probe item
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
