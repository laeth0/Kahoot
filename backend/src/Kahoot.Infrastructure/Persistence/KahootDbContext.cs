using Kahoot.Application.Common.Abstractions;
using Kahoot.Domain.Games;
using Kahoot.Domain.Hosts;
using Kahoot.Domain.Quizzes;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Infrastructure.Persistence;

public sealed class KahootDbContext(DbContextOptions<KahootDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Host> Hosts => Set<Host>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Quiz> Quizzes => Set<Quiz>();

    public DbSet<Question> Questions => Set<Question>();

    public DbSet<Choice> Choices => Set<Choice>();

    public DbSet<GameSession> GameSessions => Set<GameSession>();

    public DbSet<Participant> Participants => Set<Participant>();

    public DbSet<Answer> Answers => Set<Answer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KahootDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp with time zone");
        configurationBuilder.Properties<DateTime?>().HaveColumnType("timestamp with time zone");
    }
}
