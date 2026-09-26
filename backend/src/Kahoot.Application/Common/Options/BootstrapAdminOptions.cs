namespace Kahoot.Application.Common.Options;

public sealed class BootstrapAdminOptions
{
    public const string SectionName = "BootstrapAdmin";

    public bool Enabled { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
