namespace Kahoot.Application.IntegrationTests.TestSupport;

using System.Text.RegularExpressions;
using Kahoot.Infrastructure.Persistence;
using Kahoot.Infrastructure.ServiceCollectionExtension;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

public sealed class DatabaseSandbox : IAsyncDisposable
{
    private static readonly Regex DatabaseNameRegex = new("^[a-z0-9_]{32,64}$", RegexOptions.Compiled);

    private readonly string _adminConnectionString;
    private readonly string _databaseName;
    private readonly string _connectionString;
    private bool _initialized;
    private bool _disposed;

    public DatabaseSandbox(string adminConnectionString)
    {
        _adminConnectionString = adminConnectionString;
        _databaseName = $"kahoot_it_{Guid.NewGuid():N}";

        if (!DatabaseNameRegex.IsMatch(_databaseName))
        {
            throw new InvalidOperationException($"Generated database name '{_databaseName}' is not a valid identifier.");
        }

        NpgsqlConnectionStringBuilder appConnBuilder = new(_adminConnectionString)
        {
            Database = _databaseName,
            ApplicationName = "KahootIntegrationTest",
            MinPoolSize = 0,
            MaxPoolSize = 20,
            Timeout = 15,
            CommandTimeout = 30,
            IncludeErrorDetail = false
        };

        _connectionString = appConnBuilder.ConnectionString;
    }

    public string DatabaseName => _databaseName;

    public string ConnectionString => _connectionString;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await using (NpgsqlConnection adminConn = new(_adminConnectionString))
        {
            await adminConn.OpenAsync(cancellationToken);
            await using (NpgsqlCommand createCmd = adminConn.CreateCommand())
            {
                createCmd.CommandText = $"CREATE DATABASE \"{_databaseName}\";";
                await createCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        Dictionary<string, string?> inMemorySettings = new()
        {
            ["ConnectionStrings:DefaultConnection"] = _connectionString,
            ["Database:CommandTimeoutSeconds"] = "30",
            ["Database:MigrationCommandTimeoutSeconds"] = "120",
            ["Database:EnableSensitiveDataLogging"] = "false",
            ["Database:EnableDetailedErrors"] = "false"
        };

        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<Kahoot.Application.Common.Interfaces.ICurrentUser>(new TestCurrentUser(TestCaller.Anonymous));
        services.AddPersistence(config);

        await using (ServiceProvider migrationProvider = services.BuildServiceProvider())
        {
            using (IServiceScope scope = migrationProvider.CreateScope())
            {
                AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await dbContext.Database.MigrateAsync(cancellationToken);

                IEnumerable<string> pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
                if (pendingMigrations.Any())
                {
                    throw new InvalidOperationException($"Pending migrations remain after migration: {string.Join(", ", pendingMigrations)}");
                }
            }
        }

        _initialized = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (!_initialized)
        {
            return;
        }

        try
        {
            await using NpgsqlConnection adminConn = new(_adminConnectionString);
            await adminConn.OpenAsync();

            await using (NpgsqlCommand terminateCmd = adminConn.CreateCommand())
            {
                terminateCmd.CommandText = "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @dbName AND pid <> pg_backend_pid();";
                terminateCmd.Parameters.AddWithValue("@dbName", _databaseName);
                await terminateCmd.ExecuteNonQueryAsync();
            }

            await using (NpgsqlCommand dropCmd = adminConn.CreateCommand())
            {
                dropCmd.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\";";
                await dropCmd.ExecuteNonQueryAsync();
            }
        }
        catch
        {
            // Suppress exception during sandbox disposal to avoid hiding test failure
        }
    }
}
