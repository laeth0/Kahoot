using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Kahoot.Infrastructure.Persistence;

public sealed class KahootDbContextFactory : IDesignTimeDbContextFactory<KahootDbContext>
{
    private const string ApiProjectName = "Kahoot.Api";

    public KahootDbContext CreateDbContext(string[] args)
    {
        string apiProjectDirectory = ResolveApiProjectDirectory();

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        string? envConnection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        string connectionString = !string.IsNullOrWhiteSpace(envConnection)
            ? envConnection
            : configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    $"Connection string 'DefaultConnection' was not found in {ApiProjectName}/appsettings.json.");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is empty. Set ConnectionStrings__DefaultConnection environment variable.");
        }

        DbContextOptions<KahootDbContext> options = new DbContextOptionsBuilder<KahootDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new KahootDbContext(options);
    }

    private static string ResolveApiProjectDirectory()
    {
        for (DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
             directory is not null;
             directory = directory.Parent)
        {
            if (directory.Name == ApiProjectName && File.Exists(Path.Combine(directory.FullName, "appsettings.json")))
            {
                return directory.FullName;
            }

            string nested = Path.Combine(directory.FullName, "src", ApiProjectName);
            if (File.Exists(Path.Combine(nested, "appsettings.json")))
            {
                return nested;
            }
        }

        throw new InvalidOperationException(
            $"Could not locate src/{ApiProjectName}/appsettings.json for design-time configuration.");
    }
}
