namespace Kahoot.Application.Common.Results;

public sealed record Error(
    string Code,
    string Description,
    ErrorType Type)
{
    public static readonly Error None =
        new(
            string.Empty,
            string.Empty,
            ErrorType.None);

    public static Error NotFound(
        string code,
        string description) =>
        new(
            code,
            description,
            ErrorType.NotFound);

    public static Error Validation(
        string code,
        string description) =>
        new(
            code,
            description,
            ErrorType.Validation);

    public static Error Conflict(
        string code,
        string description) =>
        new(
            code,
            description,
            ErrorType.Conflict);

    public static Error Unauthorized(
        string code,
        string description) =>
        new(
            code,
            description,
            ErrorType.Unauthorized);

    public static Error Forbidden(
        string code,
        string description) =>
        new(
            code,
            description,
            ErrorType.Forbidden);

    public static Error RateLimited(
        string code,
        string description) =>
        new(
            code,
            description,
            ErrorType.RateLimited);

    public static Error Unavailable(
        string code,
        string description) =>
        new(
            code,
            description,
            ErrorType.Unavailable);

    public static Error Failure(
        string code,
        string description) =>
        new(
            code,
            description,
            ErrorType.Failure);
}
