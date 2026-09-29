namespace Kahoot.Application.Features.Games.GetGameParticipants;

using FluentValidation;

public sealed class GetGameParticipantsQueryValidator : AbstractValidator<GetGameParticipantsQuery>
{
    public GetGameParticipantsQueryValidator()
    {
        RuleFor(query => query.GameId)
            .NotEmpty()
            .WithMessage("GameId is required.");

        RuleFor(query => query.Limit)
            .InclusiveBetween(1, 500)
            .When(query => query.Limit.HasValue)
            .WithMessage("Limit must be between 1 and 500.");

        RuleFor(query => query.Cursor)
            .GreaterThan(0)
            .When(query => query.Cursor.HasValue)
            .WithMessage("Cursor must be a positive seat number.");
    }
}
