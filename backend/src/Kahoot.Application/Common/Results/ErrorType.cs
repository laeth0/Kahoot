namespace Kahoot.Application.Common.Results;

public enum ErrorType
{
    // No Error - Sentinel value representing an operation without failure
    None,

    // Internal Failure - Unhandled or unclassified operational error (HTTP 500)
    Failure,

    // Validation Error - Input validation constraint failure (HTTP 400)
    Validation,

    // Resource Not Found - Requested entity does not exist or outside tenant scope (HTTP 404)
    NotFound,

    // State Conflict - Optimistic concurrency, active session guard, or invariant clash (HTTP 409)
    Conflict,

    // Authentication Failure - Missing, invalid, expired, or revoked credentials (HTTP 401)
    Unauthorized,

    // Authorization Failure - Authenticated user lacks required permissions or role (HTTP 403)
    Forbidden,

    // Rate Limit Exceeded - Request quota exhausted on endpoint (HTTP 429)
    RateLimited,

    // Service Unavailable - Downstream dependency or background queue unavailable (HTTP 503)
    Unavailable,

    // Payload Too Large - Upload file or request body exceeds bounded limit (HTTP 413)
    TooLarge,

    // Unsupported Media Type - Invalid or disallowed MIME format (HTTP 415)
    UnsupportedType
}
