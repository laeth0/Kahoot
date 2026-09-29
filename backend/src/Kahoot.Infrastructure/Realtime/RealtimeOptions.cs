namespace Kahoot.Infrastructure.Realtime;

public sealed class RealtimeOptions
{
    public const string SectionName = "Realtime";

    public string RedisConnectionString { get; set; } = string.Empty;

    public string ChannelPrefix { get; set; } = string.Empty;

    public static bool HasValidChannelPrefix(RealtimeOptions options) =>
        options.ChannelPrefix.Length is > 0 and <= 64 &&
        options.ChannelPrefix.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
