namespace Kahoot.Api.UnitTests.TestSupport;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Application.Common.Persistence;
using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

public sealed class FailOnUseDbContext : IAppDbContext
{
    private static InvalidOperationException Fail() =>
        new("Unexpected access to persistence layer in isolated test.");

    public DbSet<User> Users => throw Fail();

    public DbSet<RefreshToken> RefreshTokens => throw Fail();

    public DbSet<Quiz> Quizzes => throw Fail();

    public DbSet<QuestionImage> QuestionImages => throw Fail();

    public DbSet<Question> Questions => throw Fail();

    public DbSet<Choice> Choices => throw Fail();

    public DbSet<Game> Games => throw Fail();

    public DbSet<GameQuestionSnapshot> GameQuestionSnapshots => throw Fail();

    public DbSet<GameChoiceSnapshot> GameChoiceSnapshots => throw Fail();

    public DbSet<Participant> Participants => throw Fail();

    public DbSet<ParticipantSessionToken> ParticipantSessionTokens => throw Fail();

    public DbSet<AnswerSubmission> AnswerSubmissions => throw Fail();

    public DbSet<AnswerSubmissionChoice> AnswerSubmissionChoices => throw Fail();

    public DbSet<GameCommandIdempotency> GameCommandIdempotencies => throw Fail();

    public DatabaseFacade Database => throw Fail();

    public Task<User?> GetUserForUpdateAsync(Guid userId, CancellationToken cancellationToken) => throw Fail();

    public Task<Game?> GetGameForUpdateAsync(Guid gameId, Guid hostAccountId, CancellationToken cancellationToken) => throw Fail();

    public Task<Game?> GetGameByPinForUpdateAsync(string pin, CancellationToken cancellationToken) => throw Fail();

    public Task<List<User>> GetActiveAdministratorsForUpdateAsync(CancellationToken cancellationToken) => throw Fail();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw Fail();

    public void ClearTrackedChanges() => throw Fail();
}
