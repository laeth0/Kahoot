using FluentValidation;
using Kahoot.Application.Common.Pagination;

namespace Kahoot.Application.Features.Admin.Users.ListUsers;

public sealed class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersQueryValidator()
    {
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");

        RuleFor(query => query.Username)
            .MaximumLength(64)
            .WithMessage("Username search filter cannot exceed 64 characters.")
            .When(query => query.Username is not null);

        RuleFor(query => query.Cursor)
            .Must(cursor => string.IsNullOrWhiteSpace(cursor) || KeysetCursor.TryDecode(cursor, out _))
            .WithMessage("Cursor is invalid or malformed.");
    }
}
