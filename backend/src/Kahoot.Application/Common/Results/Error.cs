namespace Kahoot.Application.Common.Results;

public sealed record Error(
    string Code,
    string Description,
    ErrorType Type)
{
    // Sentinel Empty Error - Returned on successful results
    public static readonly Error None =
        new(
            string.Empty,
            string.Empty,
            ErrorType.None);

    // 404 Not Found Factory - Resource or entity not found within tenant boundary
    public static Error NotFound(
        string code,
        string description) =>
        new(
            code,
            description,
            ErrorType.NotFound);

    // 400 Bad Request Factory - Validation or domain invariant violation
    public static Error Validation(
        string code,
        string description) =>
        new(
            code,
            description,
            ErrorType.Validation);

    // 409 Conflict Factory - Optimistic concurrency, state machine conflict, or duplicate key
    public static Error Conflict(
        string code,
        string description) =>
        new(
            code,
            description,
            ErrorType.Conflict);

    // 401 Unauthorized Factory - Authentication missing, invalid, or expired
    public static Error Unauthorized(
        string code,
        string description) =>
        new(
            code,
            description,
            ErrorType.Unauthorized);

    // 403 Forbidden Factory - Caller lacks administrative or tenant privileges
    public static Error Forbidden(
        string code,
        string description) =>
        new(
            code,
            description,
            ErrorType.Forbidden);

    // 429 Rate Limited Factory - Rate limit threshold exceeded
    public static Error RateLimited(
        string code,
        string description) =>
        new(
            code,
            description,
            ErrorType.RateLimited);

    // 503 Unavailable Factory - Downstream service, worker, or channel unavailable
    public static Error Unavailable(
        string code,
        string description) =>
        new(
            code,
            description,
            ErrorType.Unavailable);

    // 500 Failure Factory - Internal unclassified failure
    public static Error Failure(
        string code,
        string description) =>
        new(
            code,
            description,
            ErrorType.Failure);

    // 413 Payload Too Large Factory - File or request exceeds size bounds
    public static Error TooLarge(
        string code,
        string description) =>
        new(
            code,
            description,
            ErrorType.TooLarge);

    // 415 Unsupported Media Type Factory - MIME type not allowed
    public static Error UnsupportedType(
        string code,
        string description) =>
        new(
            code,
            description,
            ErrorType.UnsupportedType);
}
