using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Kahoot.Application.Common.Persistence;

public interface IAppDbContext
{
    DbSet<User> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<Quiz> Quizzes { get; }

    DbSet<MediaItem> MediaItems { get; }

    DbSet<Question> Questions { get; }

    DbSet<Choice> Choices { get; }

    DbSet<Game> Games { get; }

    DbSet<GameQuestionSnapshot> GameQuestionSnapshots { get; }

    DbSet<GameChoiceSnapshot> GameChoiceSnapshots { get; }

    DbSet<Participant> Participants { get; }

    DbSet<ParticipantSessionToken> ParticipantSessionTokens { get; }

    DbSet<AnswerSubmission> AnswerSubmissions { get; }

    DbSet<AnswerSubmissionChoice> AnswerSubmissionChoices { get; }

    DbSet<GameCommandIdempotency> GameCommandIdempotencies { get; }

    DatabaseFacade Database { get; }

    Task<User?> GetUserForUpdateAsync(Guid userId, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
