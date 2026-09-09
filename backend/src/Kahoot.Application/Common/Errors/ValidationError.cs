using Kahoot.Domain.Common;

namespace Kahoot.Application.Common.Errors;

public sealed record ValidationError(IReadOnlyDictionary<string, string[]> Errors)
    : Error("Validation.Failed", "One or more validation errors occurred.");
