using Kahoot.Domain.Common;

namespace Kahoot.Application.Common.Errors;

public static class SharedErrors
{
    public static readonly Error Unauthorized = new("Auth.Unauthorized", "Authentication is required.");

    public static readonly Error Forbidden = new("Auth.Forbidden", "You do not have access to this resource.");

    public static Error NotFound(string resource) =>
        new($"{resource}.NotFound", $"The requested {resource.ToLowerInvariant()} was not found.");
}
