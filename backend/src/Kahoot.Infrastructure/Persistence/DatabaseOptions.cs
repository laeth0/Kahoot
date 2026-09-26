namespace Kahoot.Infrastructure.Persistence;

/// <summary>
/// Strongly typed options for EF Core / Npgsql database behavior.
/// The connection string is populated from ConnectionStrings:DefaultConnection,
/// not from this section, so it continues to follow the standard
/// ASP.NET Core ConnectionStrings convention and can be overridden in
/// Production via ConnectionStrings__DefaultConnection environment variable.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Populated in DI from ConnectionStrings:DefaultConnection.
    /// Not stored in the Database: config section.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Enables EF Core detailed error messages (recommended only in Development).
    /// </summary>
    public bool EnableDetailedErrors { get; set; }

    /// <summary>
    /// Enables logging of sensitive data such as SQL parameter values.
    /// Must never be true in Production.
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; }

    /// <summary>
    /// Command execution timeout in seconds. Explicit unit name prevents ambiguity.
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Per-command timeout for startup migrations, separate from request-time database commands.
    /// </summary>
    public int MigrationCommandTimeoutSeconds { get; set; } = 300;
}
