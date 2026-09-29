namespace Kahoot.Application.Features.Games.GetJoinInfo;

using System.Text.RegularExpressions;
using FluentValidation;

public sealed class GetJoinInfoQueryValidator : AbstractValidator<GetJoinInfoQuery>
{
    private static readonly Regex PinRegex = new(@"^[0-9]{4,8}$", RegexOptions.Compiled);

    public GetJoinInfoQueryValidator()
    {
        RuleFor(query => query.Pin)
            .NotEmpty()
            .WithMessage("PIN is required.")
            .Must(pin => !string.IsNullOrWhiteSpace(pin) && PinRegex.IsMatch(pin))
            .WithMessage("PIN must be 4 to 8 numeric digits.");
    }
}
