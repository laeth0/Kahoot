namespace Kahoot.Infrastructure.Startup;

public sealed class HostSeedOptions
{
    public const string SectionName = "Seeding:Host";

    public bool Enabled { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
}
