using FluentValidation;
using Kahoot.Application.Common.Pagination;

namespace Kahoot.Application.Features.Quizzes.ListQuizzes;

public sealed class ListQuizzesQueryValidator : AbstractValidator<ListQuizzesQuery>
{
    private const int MinPageSize = 1;
    private const int MaxPageSize = 100;

    public ListQuizzesQueryValidator()
    {
        // Bounded Page Size (DoS Defense) - Restricts page size between 1 and 100 to prevent memory exhaustion
        RuleFor(query => query.PageSize)
            .InclusiveBetween(MinPageSize, MaxPageSize)
            .WithMessage($"PageSize must be between {MinPageSize} and {MaxPageSize}.");

        // Keyset Cursor Validation - Validates base64 composite cursor structure
        RuleFor(query => query.Cursor)
            .Must(cursor => string.IsNullOrWhiteSpace(cursor) || KeysetCursor.TryDecode(cursor, out _))
            .WithMessage("Cursor is invalid or malformed.");
    }
}
