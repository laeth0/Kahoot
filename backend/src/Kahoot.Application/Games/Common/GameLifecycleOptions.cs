namespace Kahoot.Application.Games.Common;

public sealed class GameLifecycleOptions
{
    public const string SectionName = "GameLifecycle";

    public int HostDisconnectGracePeriodSeconds { get; set; } = 15;
}
