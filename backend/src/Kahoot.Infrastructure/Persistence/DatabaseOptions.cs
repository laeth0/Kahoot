namespace Kahoot.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    // Configuration Section Name - Key identifying database settings in appsettings
    public const string SectionName = "Database";

    // Primary PostgreSQL Connection String - Formats credentials, pool size, host, and port
    public string ConnectionString { get; set; } = string.Empty;

    // Diagnostic Flag - Enables verbose EF Core query error details in non-production environments
    public bool EnableDetailedErrors { get; set; }

    // Security Flag - Controls whether parameter values are included in EF Core logs (disabled in production)
    public bool EnableSensitiveDataLogging { get; set; }

    // Standard Query Timeout - Maximum duration in seconds for application SQL queries (30s)
    public int CommandTimeoutSeconds { get; set; } = 30;

    // DDL Migration Timeout - Extended timeout in seconds for schema migrations and index building (300s)
    public int MigrationCommandTimeoutSeconds { get; set; } = 300;
}
