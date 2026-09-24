using Kahoot.Application.Common.Persistence;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Quiz> Quizzes => Set<Quiz>();

    public DbSet<MediaItem> MediaItems => Set<MediaItem>();

    public DbSet<Question> Questions => Set<Question>();

    public DbSet<Choice> Choices => Set<Choice>();

    public DbSet<Game> Games => Set<Game>();

    public DbSet<GameQuestionSnapshot> GameQuestionSnapshots => Set<GameQuestionSnapshot>();

    public DbSet<GameChoiceSnapshot> GameChoiceSnapshots => Set<GameChoiceSnapshot>();

    public DbSet<Participant> Participants => Set<Participant>();

    public DbSet<ParticipantSessionToken> ParticipantSessionTokens => Set<ParticipantSessionToken>();

    public DbSet<AnswerSubmission> AnswerSubmissions => Set<AnswerSubmission>();

    public DbSet<AnswerSubmissionChoice> AnswerSubmissionChoices => Set<AnswerSubmissionChoice>();

    public DbSet<GameCommandIdempotency> GameCommandIdempotencies => Set<GameCommandIdempotency>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresEnum<UserRole>("user_role");
        modelBuilder.HasPostgresEnum<UserStatus>("user_status");
        modelBuilder.HasPostgresEnum<MediaStatus>("media_status");
        modelBuilder.HasPostgresEnum<GameStatus>("game_status");

        modelBuilder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);
    }
}
