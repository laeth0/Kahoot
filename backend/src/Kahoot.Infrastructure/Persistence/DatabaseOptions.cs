namespace Kahoot.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = string.Empty;

    public bool EnableDetailedErrors { get; set; }

    public bool EnableSensitiveDataLogging { get; set; }

    public int CommandTimeoutSeconds { get; set; } = 30;

    public int MigrationCommandTimeoutSeconds { get; set; } = 300;
}
