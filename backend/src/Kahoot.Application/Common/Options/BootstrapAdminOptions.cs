namespace Kahoot.Application.Common.Options;

public sealed class BootstrapAdminOptions
{
    // Configuration Section Name - Key identifying bootstrap admin settings in appsettings
    public const string SectionName = "BootstrapAdmin";

    // Seeder Feature Flag - Toggles automated administrator provisioning during database migration
    public bool Enabled { get; set; }

    // Seed Administrator Identity - Preconfigured administrative username
    public string Username { get; set; } = string.Empty;

    // Seed Administrator Secret - High-entropy initial password hashed via Argon2id during seeding
    public string Password { get; set; } = string.Empty;
}
