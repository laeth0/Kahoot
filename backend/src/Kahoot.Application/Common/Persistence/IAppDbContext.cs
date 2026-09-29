using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Kahoot.Application.Common.Persistence;

public interface IAppDbContext
{
    // User Accounts - Persisted user profiles, credentials, security versions, and administrative statuses
    DbSet<User> Users { get; }

    // Authentication Sessions - Active, rotated, and revoked refresh token records
    DbSet<RefreshToken> RefreshTokens { get; }

    // Quiz Templates - Host-owned quiz metadata and monotonic revisions
    DbSet<Quiz> Quizzes { get; }

    // Question Images - Uploaded image files, dimensions, and garbage-collection orphan timestamps
    DbSet<QuestionImage> QuestionImages { get; }

    // Quiz Questions - Questions associated with template quizzes
    DbSet<Question> Questions { get; }

    // Answer Choices - Multiple-choice options configured on quiz questions
    DbSet<Choice> Choices { get; }

    // Live Game Sessions - Authoritative game state machines, PINs, versions, and capacity
    DbSet<Game> Games { get; }

    // Immutable Question Snapshots - Deep copies of questions captured at game creation under RepeatableRead
    DbSet<GameQuestionSnapshot> GameQuestionSnapshots { get; }

    // Immutable Choice Snapshots - Deep copies of choices captured at game creation
    DbSet<GameChoiceSnapshot> GameChoiceSnapshots { get; }

    // Session Participants - Joined players, seat numbers, nickname tombstones, scores, and connection generations
    DbSet<Participant> Participants { get; }

    // Participant Session Tokens - Cryptographically hashed bearer tokens for player reconnects
    DbSet<ParticipantSessionToken> ParticipantSessionTokens { get; }

    // Player Answer Submissions - Evaluated player responses, timestamps, response latencies, and points
    DbSet<AnswerSubmission> AnswerSubmissions { get; }

    // Submission Choices Junction - Normalized mapping of choices selected per answer submission
    DbSet<AnswerSubmissionChoice> AnswerSubmissionChoices { get; }

    // Command Idempotency Log - Deduplication table caching command payloads, state versions, and JSON responses
    DbSet<GameCommandIdempotency> GameCommandIdempotencies { get; }

    // Database Facade - Direct access to raw SQL execution, connection state, and transactions
    DatabaseFacade Database { get; }

    // Pessimistic Row Lock (USER-LOCK-001) - Acquires SELECT FOR UPDATE on User row to serialize profile, role, or quiz mutations
    Task<User?> GetUserForUpdateAsync(Guid userId, CancellationToken cancellationToken);

    // Pessimistic Row Lock (GAME-LOCK-001) - Acquires SELECT FOR UPDATE on Game row scoped by host tenant to serialize state transitions
    Task<Game?> GetGameForUpdateAsync(Guid gameId, Guid hostAccountId, CancellationToken cancellationToken);

    // Pessimistic Row Lock (JOIN-LOCK-001) - Acquires SELECT FOR UPDATE on Game row by PIN to enforce 500-seat transactional capacity
    Task<Game?> GetGameByPinForUpdateAsync(string pin, CancellationToken cancellationToken);

    // Pessimistic Table/Row Lock (ADMIN-LOCK-001) - Acquires SELECT FOR UPDATE on all active admins to prevent zero-admin lockout
    Task<List<User>> GetActiveAdministratorsForUpdateAsync(CancellationToken cancellationToken);

    // Atomic Unit of Work - Flushes all tracked entity modifications in a single atomic database transaction
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
