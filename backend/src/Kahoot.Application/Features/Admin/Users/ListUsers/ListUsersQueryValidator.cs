using FluentValidation;
using Kahoot.Application.Common.Pagination;

namespace Kahoot.Application.Features.Admin.Users.ListUsers;

public sealed class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersQueryValidator()
    {
        // Bounded Page Size (ACCT-BOUND-003) - Restricts pagination limit to between 1 and 100 to prevent DoS memory exhaustion
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");

        // Bounded Filter Length (ACCT-BOUND-002) - Limits search username prefix to at most 64 characters
        RuleFor(query => query.Username)
            .MaximumLength(64)
            .WithMessage("Username search filter cannot exceed 64 characters.")
            .When(query => query.Username is not null);

        // Enum Range Validation - Guarantees lifecycle status filter matches supported domain values
        RuleFor(query => query.Status)
            .IsInEnum()
            .When(query => query.Status.HasValue);

        // Keyset Cursor Validation (ACCT-ERR-001) - Validates opaque cursor format to reject malformed or tampered pagination tokens
        RuleFor(query => query.Cursor)
            .Must(cursor => string.IsNullOrWhiteSpace(cursor) || KeysetCursor.TryDecode(cursor, out _))
            .WithMessage("Cursor is invalid or malformed.");
    }
}
