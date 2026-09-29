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

    public DbSet<QuestionImage> QuestionImages => Set<QuestionImage>();

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

    // Pessimistic Row Lock (USER-LOCK-001) - Acquires raw PostgreSQL 'FOR UPDATE' row-level lock on user record
    public async Task<User?> GetUserForUpdateAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Parameterized Raw SQL Query - Executes SELECT FOR UPDATE directly on primary key index with AsNoTracking
        List<User> users = await Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId} FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return users.Count == 0 ? null : users[0];
    }

    // Pessimistic Row Lock (GAME-LOCK-001) - Acquires PostgreSQL 'FOR UPDATE' row-level lock scoped by host tenant
    public async Task<Game?> GetGameForUpdateAsync(
        Guid gameId, Guid hostAccountId, CancellationToken cancellationToken)
    {
        // Tenant-Scoped Locking Query - Locks game row to serialize concurrent state machine commands
        List<Game> games = await Games
            .FromSqlInterpolated($"SELECT * FROM games WHERE id = {gameId} AND host_account_id = {hostAccountId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return games.Count == 0 ? null : games[0];
    }

    // Pessimistic Capacity Lock (JOIN-LOCK-001) - Locks game row by PIN to enforce 500-player join capacity ceiling
    public async Task<Game?> GetGameByPinForUpdateAsync(string pin, CancellationToken cancellationToken)
    {
        // PIN Index Lookup With Row Lock - Locks non-finished game session row during lobby joining
        List<Game> games = await Games
            .FromSqlInterpolated($"SELECT * FROM games WHERE pin = {pin} AND status != {GameStatus.Finished} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return games.Count == 0 ? null : games[0];
    }

    // Pessimistic Ordered Table Lock (ADMIN-LOCK-001) - Locks all active administrators ordered by ID to eliminate deadlocks
    public async Task<List<User>> GetActiveAdministratorsForUpdateAsync(CancellationToken cancellationToken)
    {
        // Deterministic Lock Ordering - ORDER BY id prevents circular wait deadlocks across concurrent admin management commands
        return await Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE role = {UserRole.SystemAdmin} AND status = {UserStatus.Active} ORDER BY id FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    // Model Configuration Binding - Discovers and applies all EF Core IEntityTypeConfiguration classes in assembly
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);
    }

    // Atomic Unit of Work & Exception Translation - Translates vendor-specific PostgreSQL SQLSTATE codes to domain exceptions
    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        // Unique Constraint Collision Interception - Maps PostgreSQL 23505 to vendor-agnostic UniqueConstraintViolationException
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException postgresException &&
                                           postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new UniqueConstraintViolationException(
                postgresException.ConstraintName,
                ex);
        }
        // Foreign Key Collision Interception - Maps PostgreSQL 23503 to vendor-agnostic ForeignKeyConstraintViolationException
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException postgresException &&
                                           postgresException.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new ForeignKeyConstraintViolationException(
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
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException postgresException &&
                                           postgresException.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new ForeignKeyConstraintViolationException(
                postgresException.ConstraintName,
                ex);
        }
    }

    public override int SaveChanges()
    {
        return SaveChanges(acceptAllChangesOnSuccess: true);
    }

    public void ClearTrackedChanges()
    {
        ChangeTracker.Clear();
    }
}
