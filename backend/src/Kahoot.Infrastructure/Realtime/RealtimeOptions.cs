namespace Kahoot.Infrastructure.Realtime;

// Realtime Subsystem Configuration - Defines connection parameters and channel prefix boundaries for Redis pub/sub and presence tracking.
public sealed class RealtimeOptions
{
    public const string SectionName = "Realtime";

    // Redis Connection String - Endpoint configuration for the Redis cluster or standalone instance.
    public string RedisConnectionString { get; set; } = string.Empty;

    // Redis Channel Prefix - Key and channel namespace preventing collision in shared Redis deployments.
    public string ChannelPrefix { get; set; } = string.Empty;

    // Channel Prefix Validation - Restricts prefix to 1-64 alphanumeric characters, underscores, and hyphens.
    public static bool HasValidChannelPrefix(RealtimeOptions options) =>
        options.ChannelPrefix.Length is > 0 and <= 64 &&
        options.ChannelPrefix.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
