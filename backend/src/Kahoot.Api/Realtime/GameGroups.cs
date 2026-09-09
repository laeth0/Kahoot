namespace Kahoot.Api.Realtime;

internal static class GameGroups
{
    public static string Players(Guid gameId) => $"game:{gameId}";

    public static string Host(Guid gameId) => $"game:{gameId}:host";
}
