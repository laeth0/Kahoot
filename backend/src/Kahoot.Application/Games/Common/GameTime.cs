namespace Kahoot.Application.Games.Common;

internal static class GameTime
{
    public static DateTimeOffset ToUtcOffset(this DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
