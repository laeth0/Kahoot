namespace Kahoot.Application.Features.Games.GetJoinInfo;

using System.Text.RegularExpressions;
using FluentValidation;

public sealed class GetJoinInfoQueryValidator : AbstractValidator<GetJoinInfoQuery>
{
    // Regex Compilation Optimization - Precompiles numeric pattern machine into IL to prevent JIT penalty on high join throughput
    private static readonly Regex PinRegex = new(@"^[0-9]{4,8}$", RegexOptions.Compiled);

    public GetJoinInfoQueryValidator()
    {
        // Fail-Fast Boundary Validation - Validates PIN format before querying database index
        RuleFor(query => query.Pin)
            .NotEmpty()
            .WithMessage("PIN is required.")
            .Must(pin => !string.IsNullOrWhiteSpace(pin) && PinRegex.IsMatch(pin))
            .WithMessage("PIN must be 4 to 8 numeric digits.");
    }
}
