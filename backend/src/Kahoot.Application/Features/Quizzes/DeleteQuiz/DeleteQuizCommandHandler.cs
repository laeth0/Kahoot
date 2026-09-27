using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Features.Quizzes.DeleteQuiz;

public sealed class DeleteQuizCommandHandler : ICommandHandler<DeleteQuizCommand>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public DeleteQuizCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(
        DeleteQuizCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        Quiz? quiz = await _dbContext.Quizzes
            .TagWith("Quizzes:DeleteQuiz:FindQuiz")
            .Where(q => q.Id == request.QuizId && q.HostAccountId == hostAccountId)
            .SingleOrDefaultAsync(cancellationToken);

        if (quiz is null)
        {
            return Result.Failure(QuizErrors.NotFound);
        }

        List<GameStatus> gameStatuses = await _dbContext.Games
            .AsNoTracking()
            .TagWith("Quizzes:DeleteQuiz:GetGameStatuses")
            .Where(game => game.SourceQuizId == quiz.Id && game.HostAccountId == hostAccountId)
            .Select(game => game.Status)
            .ToListAsync(cancellationToken);

        if (gameStatuses.Any(status => status != GameStatus.Finished))
        {
            return Result.Failure(QuizErrors.InUse);
        }

        if (gameStatuses.Count > 0)
        {
            return Result.Failure(QuizErrors.HasSessions);
        }

        List<Guid> mediaIdsInQuiz = await _dbContext.Questions
            .TagWith("Quizzes:DeleteQuiz:GetMediaIds")
            .Where(q => q.QuizId == quiz.Id && q.HostAccountId == hostAccountId && q.MediaItemId.HasValue)
            .Select(q => q.MediaItemId!.Value)
            .ToListAsync(cancellationToken);

        if (mediaIdsInQuiz.Count > 0)
        {
            Dictionary<Guid, int> mediaReferenceCounts = mediaIdsInQuiz
                .GroupBy(id => id)
                .ToDictionary(g => g.Key, g => g.Count());

            List<Guid> distinctMediaIds = mediaReferenceCounts.Keys.ToList();

            List<MediaItem> mediaItems = await _dbContext.MediaItems
                .TagWith("Quizzes:DeleteQuiz:ReconcileMediaReferences")
                .Where(m => distinctMediaIds.Contains(m.Id) && m.HostAccountId == hostAccountId)
                .ToListAsync(cancellationToken);

            DateTimeOffset utcNow = _timeProvider.GetUtcNow();

            foreach (MediaItem media in mediaItems)
            {
                int countToRemove = mediaReferenceCounts[media.Id];
                media.ReferenceCount = Math.Max(0, media.ReferenceCount - countToRemove);
                if (media.ReferenceCount == 0)
                {
                    media.UnreferencedSince = utcNow;
                }
            }
        }

        _dbContext.Quizzes.Remove(quiz);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
