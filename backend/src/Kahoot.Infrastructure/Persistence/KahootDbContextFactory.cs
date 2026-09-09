using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kahoot.Infrastructure.Persistence;

public sealed class KahootDbContextFactory : IDesignTimeDbContextFactory<KahootDbContext>
{
    private const string FallbackConnectionString =
        "Host=localhost;Port=5432;Database=kahoot;Username=postgres;Password=postgres";

    public KahootDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? FallbackConnectionString;

        DbContextOptions<KahootDbContext> options = new DbContextOptionsBuilder<KahootDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new KahootDbContext(options);
    }
}
