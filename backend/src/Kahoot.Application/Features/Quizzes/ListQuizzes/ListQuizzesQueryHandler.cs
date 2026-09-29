using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Pagination;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Features.Quizzes.ListQuizzes;

public sealed class ListQuizzesQueryHandler : IQueryHandler<ListQuizzesQuery, ListQuizzesResponse>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public ListQuizzesQueryHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<Result<ListQuizzesResponse>> Handle(
        ListQuizzesQuery query,
        CancellationToken cancellationToken)
    {
        // Tenant Isolation - Authenticates host session before enumerating quizzes
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<ListQuizzesResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        // Query Optimization: AsNoTracking - Disables EF change tracker for high-throughput pagination
        // Observability Tagging - Instruments SQL query with TagWith for APM distributed tracing
        // Multi-Tenant Isolation - Scopes quiz listing strictly to host tenant boundary
        IQueryable<Quiz> queryable = _dbContext.Quizzes
            .AsNoTracking()
            .TagWith("Quizzes:ListQuizzes")
            .Where(quiz => quiz.HostAccountId == hostAccountId);

        // Keyset Cursor Seek (QUIZ-QUERY-001) - Decodes opaque cursor and seeks via compound index (CreatedAt, Id) avoiding O(N) OFFSET scan
        if (!string.IsNullOrWhiteSpace(query.Cursor) &&
            KeysetCursor.TryDecode(query.Cursor, out KeysetCursor? cursor) &&
            cursor is not null)
        {
            queryable = queryable.Where(quiz =>
                quiz.CreatedAt < cursor.CreatedAt ||
                (quiz.CreatedAt == cursor.CreatedAt && quiz.Id < cursor.Id));
        }

        // Deterministic Composite Sorting - Guarantees stable pagination order on (created_at DESC, id DESC) matching index
        queryable = queryable
            .OrderByDescending(quiz => quiz.CreatedAt)
            .ThenByDescending(quiz => quiz.Id);

        // Bounded Page Over-Fetching (Limit + 1) - Reads one extra record to detect next page existence without separate COUNT(*) query
        int fetchLimit = query.PageSize + 1;

        // Correlated Subquery Aggregation - Computes question count directly inside PostgreSQL in single query
        List<QuizSummaryResponse> items = await queryable
            .Take(fetchLimit)
            .Select(quiz => new QuizSummaryResponse(
                quiz.Id,
                quiz.Title,
                quiz.Description,
                quiz.Revision,
                _dbContext.Questions.Count(question => question.QuizId == quiz.Id && question.HostAccountId == hostAccountId),
                quiz.CreatedAt,
                quiz.UpdatedAt))
            .ToListAsync(cancellationToken);

        // Next Page Cursor Extraction - Encodes opaque cursor from last item of requested window and trims extra probe item
        bool hasMore = items.Count > query.PageSize;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        string? nextCursor = hasMore && items.Count > 0
            ? KeysetCursor.Encode(items[^1].CreatedAt, items[^1].Id)
            : null;

        ListQuizzesResponse response = new ListQuizzesResponse(
            items,
            nextCursor,
            hasMore);

        return Result.Success(response);
    }
}
