using Kahoot.Application.Common.Exceptions;
using Kahoot.Application.Common.Persistence;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

    public async Task<User?> GetUserForUpdateAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Call inside a transaction before changing a user's refresh tokens or credentials.
        // The account row is the shared lock across application instances.
        var users = await Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId} FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return users.Count == 0 ? null : users[0];
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException postgresException &&
                                           postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new UniqueConstraintViolationException(
                postgresException.ConstraintName,
                ex);
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException postgresException &&
                                           postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new UniqueConstraintViolationException(
                postgresException.ConstraintName,
                ex);
        }
    }

    public override int SaveChanges()
    {
        return SaveChanges(acceptAllChangesOnSuccess: true);
    }
}
