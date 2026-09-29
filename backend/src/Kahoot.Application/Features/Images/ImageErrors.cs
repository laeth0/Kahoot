namespace Kahoot.Application.Features.Images;

using Kahoot.Application.Common.Results;

public static class ImageErrors
{
    // Input Boundary Validation - Emitted when payload stream is missing or empty
    public static readonly Error MissingFile = Error.Validation(
        "Validation.Failed",
        "An image file must be provided.");

    // Sanitization & Dimension Guard - Triggered on corrupted headers, decode RAM > 64MiB, or dimensions > 4096px (IMG-BOUND-002, IMG-ERR-001)
    public static readonly Error InvalidImage = Error.Validation(
        "Image.InvalidImage",
        "The image file is corrupt, invalid, or violates dimension or memory limits.");

    // Payload Boundary Enforcement - Rejects upload payloads exceeding the 5 MiB ceiling (IMG-BOUND-001, IMG-ERR-004)
    public static readonly Error TooLarge = Error.TooLarge(
        "Image.TooLarge",
        "The image file exceeds the maximum allowed payload size of 5 MiB.");

    // Format Allowlist Barrier - Rejects disallowed formats; strictly permits JPEG, PNG, and WebP (IMG-ERR-005)
    public static readonly Error UnsupportedType = Error.UnsupportedType(
        "Image.UnsupportedType",
        "The image file type is unsupported. Only JPEG, PNG, and WebP images are allowed.");

    // Storage Capacity & Availability Guard - Fails fast when storage volume free space falls below 10% (IMG-ERR-006, IMG-SEC-003)
    public static readonly Error StorageUnavailable = Error.Unavailable(
        "Image.StorageUnavailable",
        "Image storage is currently unavailable or storage capacity is below safe limits.");

    // Immutable Delivery Lookup - Emitted when requested image file is missing or traversal token is detected (IMG-ERR-003, IMG-SEC-002)
    public static readonly Error NotFound = Error.NotFound(
        "Image.NotFound",
        "The requested image was not found.");
}
