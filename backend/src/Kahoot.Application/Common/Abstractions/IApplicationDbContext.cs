using Kahoot.Domain.Games;
using Kahoot.Domain.Hosts;
using Kahoot.Domain.Quizzes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Kahoot.Application.Common.Abstractions;

public interface IApplicationDbContext
{
    DbSet<Host> Hosts { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<Quiz> Quizzes { get; }

    DbSet<Question> Questions { get; }

    DbSet<Choice> Choices { get; }

    DbSet<GameSession> GameSessions { get; }

    DbSet<Participant> Participants { get; }

    DbSet<Answer> Answers { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
