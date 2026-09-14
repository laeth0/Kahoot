using System.ComponentModel.DataAnnotations;

namespace Kahoot.Api.Observability;

public static class ObservabilityNames
{
    public const string ActivitySourceName = "Kahoot.Application";
    public const string MeterName = "Kahoot.Application";
}

public sealed class ObservabilityOptions : IValidatableObject
{
    public const string SectionName = "Observability";

    public bool Enabled { get; set; }

    [Required]
    public string ServiceName { get; set; } = "kahoot-backend";

    public Uri? OtlpEndpoint { get; set; }

    [Range(15, 300)]
    public int BusinessSnapshotIntervalSeconds { get; set; } = 30;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(ServiceName))
        {
            yield return new ValidationResult(
                "ServiceName must not be empty.",
                [nameof(ServiceName)]);
        }

        if (Enabled)
        {
            if (OtlpEndpoint is null)
            {
                yield return new ValidationResult(
                    "OtlpEndpoint must be provided when Observability is enabled.",
                    [nameof(OtlpEndpoint)]);
            }
            else if (!OtlpEndpoint.IsAbsoluteUri || (OtlpEndpoint.Scheme != Uri.UriSchemeHttp && OtlpEndpoint.Scheme != Uri.UriSchemeHttps))
            {
                yield return new ValidationResult(
                    "OtlpEndpoint must be an absolute HTTP or HTTPS URI.",
                    [nameof(OtlpEndpoint)]);
            }
        }
        else if (OtlpEndpoint is not null && (!OtlpEndpoint.IsAbsoluteUri || (OtlpEndpoint.Scheme != Uri.UriSchemeHttp && OtlpEndpoint.Scheme != Uri.UriSchemeHttps)))
        {
            yield return new ValidationResult(
                "OtlpEndpoint must be an absolute HTTP or HTTPS URI.",
                [nameof(OtlpEndpoint)]);
        }

        if (BusinessSnapshotIntervalSeconds is < 15 or > 300)
        {
            yield return new ValidationResult(
                "BusinessSnapshotIntervalSeconds must be between 15 and 300 seconds.",
                [nameof(BusinessSnapshotIntervalSeconds)]);
        }
    }
}
