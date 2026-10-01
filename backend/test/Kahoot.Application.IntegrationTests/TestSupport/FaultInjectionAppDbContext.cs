namespace Kahoot.Application.IntegrationTests.TestSupport;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Application.Common.Persistence;
using Kahoot.Domain.Entities;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

public sealed class FaultInjectionAppDbContext : IAppDbContext
{
    private readonly AppDbContext _inner;
    private readonly Func<Task>? _beforeSaveChangesAsync;

    public FaultInjectionAppDbContext(AppDbContext inner, Func<Task>? beforeSaveChangesAsync = null)
    {
        _inner = inner;
        _beforeSaveChangesAsync = beforeSaveChangesAsync;
    }

    public DbSet<User> Users => _inner.Users;

    public DbSet<RefreshToken> RefreshTokens => _inner.RefreshTokens;

    public DbSet<Quiz> Quizzes => _inner.Quizzes;

    public DbSet<QuestionImage> QuestionImages => _inner.QuestionImages;

    public DbSet<Question> Questions => _inner.Questions;

    public DbSet<Choice> Choices => _inner.Choices;

    public DbSet<Game> Games => _inner.Games;

    public DbSet<GameQuestionSnapshot> GameQuestionSnapshots => _inner.GameQuestionSnapshots;

    public DbSet<GameChoiceSnapshot> GameChoiceSnapshots => _inner.GameChoiceSnapshots;

    public DbSet<Participant> Participants => _inner.Participants;

    public DbSet<ParticipantSessionToken> ParticipantSessionTokens => _inner.ParticipantSessionTokens;

    public DbSet<AnswerSubmission> AnswerSubmissions => _inner.AnswerSubmissions;

    public DbSet<AnswerSubmissionChoice> AnswerSubmissionChoices => _inner.AnswerSubmissionChoices;

    public DbSet<GameCommandIdempotency> GameCommandIdempotencies => _inner.GameCommandIdempotencies;

    public DatabaseFacade Database => _inner.Database;

    public Task<User?> GetUserForUpdateAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _inner.GetUserForUpdateAsync(userId, cancellationToken);
    }

    public Task<Game?> GetGameForUpdateAsync(Guid gameId, Guid hostAccountId, CancellationToken cancellationToken)
    {
        return _inner.GetGameForUpdateAsync(gameId, hostAccountId, cancellationToken);
    }

    public Task<Game?> GetGameByPinForUpdateAsync(string pin, CancellationToken cancellationToken)
    {
        return _inner.GetGameByPinForUpdateAsync(pin, cancellationToken);
    }

    public Task<List<User>> GetActiveAdministratorsForUpdateAsync(CancellationToken cancellationToken)
    {
        return _inner.GetActiveAdministratorsForUpdateAsync(cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_beforeSaveChangesAsync is not null)
        {
            await _beforeSaveChangesAsync();
        }

        return await _inner.SaveChangesAsync(cancellationToken);
    }

    public void ClearTrackedChanges()
    {
        _inner.ClearTrackedChanges();
    }
}
