namespace Kahoot.Application.Features.Images;

using Kahoot.Application.Common.Results;

public static class ImageErrors
{
    public static readonly Error MissingFile = Error.Validation(
        "Validation.Failed",
        "An image file must be provided.");

    public static readonly Error InvalidImage = Error.Validation(
        "Image.InvalidImage",
        "The image file is corrupt, invalid, or violates dimension or memory limits.");

    public static readonly Error TooLarge = Error.TooLarge(
        "Image.TooLarge",
        "The image file exceeds the maximum allowed payload size of 5 MiB.");

    public static readonly Error UnsupportedType = Error.UnsupportedType(
        "Image.UnsupportedType",
        "The image file type is unsupported. Only JPEG, PNG, and WebP images are allowed.");

    public static readonly Error StorageUnavailable = Error.Unavailable(
        "Image.StorageUnavailable",
        "Image storage is currently unavailable or storage capacity is below safe limits.");

    public static readonly Error NotFound = Error.NotFound(
        "Image.NotFound",
        "The requested image was not found.");
}
