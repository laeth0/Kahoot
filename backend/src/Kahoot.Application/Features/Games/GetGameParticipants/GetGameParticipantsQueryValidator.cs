namespace Kahoot.Application.Features.Games.GetGameParticipants;

using FluentValidation;

public sealed class GetGameParticipantsQueryValidator : AbstractValidator<GetGameParticipantsQuery>
{
    public GetGameParticipantsQueryValidator()
    {
        // Resource Boundary Validation - Validates target game UUID is non-empty before query execution
        RuleFor(query => query.GameId)
            .NotEmpty()
            .WithMessage("GameId is required.");

        // Bounded Page Size (DoS Mitigation) - Prevents memory exhaustion attacks by constraining page limit between 1 and 500
        RuleFor(query => query.Limit)
            .InclusiveBetween(1, 500)
            .When(query => query.Limit.HasValue)
            .WithMessage("Limit must be between 1 and 500.");

        // Cursor Invariant Validation - Ensures keyset pagination cursor is a strictly positive seat number
        RuleFor(query => query.Cursor)
            .GreaterThan(0)
            .When(query => query.Cursor.HasValue)
            .WithMessage("Cursor must be a positive seat number.");
    }
}
