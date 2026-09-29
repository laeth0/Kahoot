namespace Kahoot.Infrastructure.Services;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Features.Games.Models;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

public sealed class GameAutoCloseService : IGameAutoCloseService
{
    private readonly IAppDbContext _dbContext;
    private readonly IGameNotificationService _notificationService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GameAutoCloseService> _logger;

    public GameAutoCloseService(
        IAppDbContext dbContext,
        IGameNotificationService notificationService,
        TimeProvider timeProvider,
        ILogger<GameAutoCloseService> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<bool> TryAutoCloseQuestionAsync(Guid gameId, CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        List<Game> games = await _dbContext.Games
            .FromSqlInterpolated($"SELECT * FROM games WHERE id = {gameId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        Game? game = games.Count == 0 ? null : games[0];

        if (game is null || game.Status != GameStatus.QuestionActive || !game.CurrentQuestionIndex.HasValue)
        {
            return false;
        }

        GameQuestionSnapshot? currentQuestion = await _dbContext.GameQuestionSnapshots
            .FirstOrDefaultAsync(
                q => q.GameId == game.Id && q.OrderIndex == game.CurrentQuestionIndex.Value,
                cancellationToken);

        if (currentQuestion is null)
        {
            return false;
        }

        if (currentQuestion.EffectiveEligibleParticipantCount <= 0)
        {
            // Initially Zero-Eligible Invariant: do not auto-close on 0 == 0.
            return false;
        }

        if (currentQuestion.AcceptedAnswerCount != currentQuestion.EffectiveEligibleParticipantCount)
        {
            return false;
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        currentQuestion.EndsAt = currentQuestion.EndsAt.HasValue && currentQuestion.EndsAt.Value < utcNow
            ? currentQuestion.EndsAt.Value
            : utcNow;
        currentQuestion.ResultsMaterializedAt = utcNow;

        List<GameChoiceSnapshot> choices = await _dbContext.GameChoiceSnapshots
            .Where(c => c.GameQuestionId == currentQuestion.Id && c.HostAccountId == game.HostAccountId)
            .OrderBy(c => c.OrderIndex)
            .ToListAsync(cancellationToken);

        List<Guid> choiceIds = choices.Select(choice => choice.Id).ToList();

        Dictionary<Guid, int> selectionCounts = await _dbContext.AnswerSubmissionChoices
            .Where(asc => asc.GameQuestionId == currentQuestion.Id && choiceIds.Contains(asc.GameChoiceId) && asc.HostAccountId == game.HostAccountId)
            .GroupBy(asc => asc.GameChoiceId)
            .Select(group => new { ChoiceId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ChoiceId, item => item.Count, cancellationToken);

        foreach (GameChoiceSnapshot choice in choices)
        {
            choice.SelectionCount = selectionCounts.TryGetValue(choice.Id, out int count) ? count : 0;
        }

        int totalAnswers = await _dbContext.AnswerSubmissions
            .CountAsync(
                sub => sub.GameId == game.Id && sub.GameQuestionId == currentQuestion.Id && sub.HostAccountId == game.HostAccountId,
                cancellationToken);

        currentQuestion.AcceptedAnswerCount = totalAnswers;

        game.Status = GameStatus.QuestionResults;
        game.StateVersion += 1;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        List<QuestionChoiceResultDto> choiceResults = choices
            .Select(c => new QuestionChoiceResultDto(c.Id, c.OrderIndex, c.Text, c.IsCorrect, c.SelectionCount))
            .ToList();

        _logger.LogInformation(
            "Question auto-closed due to all eligible participants answering. EventName={EventName} GameId={GameId} StateVersion={StateVersion} QuestionIndex={QuestionIndex} Answers={Answers}",
            "QuestionAutoClosed",
            game.Id,
            game.StateVersion,
            currentQuestion.OrderIndex,
            totalAnswers);

        await _notificationService.PublishQuestionEndedAsync(
            game.HostAccountId,
            game.Id,
            game.StateVersion,
            new QuestionEndedEvent(
                game.Id,
                game.StateVersion,
                currentQuestion.Id,
                currentQuestion.OrderIndex,
                totalAnswers,
                currentQuestion.EffectiveEligibleParticipantCount,
                utcNow,
                choiceResults.Where(choice => choice.IsCorrect).Select(choice => choice.ChoiceId).ToList(),
                choiceResults),
            CancellationToken.None);

        return true;
    }
}
