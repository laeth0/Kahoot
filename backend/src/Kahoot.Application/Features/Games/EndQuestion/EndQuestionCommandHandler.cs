namespace Kahoot.Application.Features.Games.EndQuestion;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Games.Models;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

public sealed class EndQuestionCommandHandler : ICommandHandler<EndQuestionCommand, EndQuestionResponse>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IGameCommandIdempotencyService _idempotencyService;
    private readonly IGameNotificationService _notificationService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EndQuestionCommandHandler> _logger;

    public EndQuestionCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        IGameCommandIdempotencyService idempotencyService,
        IGameNotificationService notificationService,
        TimeProvider timeProvider,
        ILogger<EndQuestionCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _idempotencyService = idempotencyService;
        _notificationService = notificationService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<EndQuestionResponse>> Handle(
        EndQuestionCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<EndQuestionResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        User? host = await _dbContext.GetUserForUpdateAsync(hostAccountId, cancellationToken);
        if (host is null || host.Role != UserRole.Host || host.Status != UserStatus.Active)
        {
            return Result.Failure<EndQuestionResponse>(AuthErrors.Unauthorized);
        }

        Game? game = await _dbContext.GetGameForUpdateAsync(request.GameId, hostAccountId, cancellationToken);
        if (game is null)
        {
            return Result.Failure<EndQuestionResponse>(GameErrors.NotFound);
        }

        IdempotencyCheckResult<EndQuestionResponse> idempotencyResult = await _idempotencyService.CheckAsync<EndQuestionResponse>(
            request.GameId,
            request.CommandId,
            nameof(EndQuestionCommand),
            request,
            cancellationToken);

        if (idempotencyResult.IsReplay)
        {
            if (idempotencyResult.Error is not null)
            {
                return Result.Failure<EndQuestionResponse>(idempotencyResult.Error);
            }

            return Result.Success(idempotencyResult.CachedResponse!);
        }

        if (game.Status == GameStatus.Finished)
        {
            return Result.Failure<EndQuestionResponse>(GameErrors.InvalidStateTransition);
        }

        if (game.HostGraceExpiresAt <= _timeProvider.GetUtcNow())
        {
            return Result.Failure<EndQuestionResponse>(GameErrors.InvalidStateTransition);
        }

        if (game.StateVersion != request.ExpectedStateVersion)
        {
            return Result.Failure<EndQuestionResponse>(GameErrors.ConcurrentModification);
        }

        if (game.Status != GameStatus.QuestionActive)
        {
            return Result.Failure<EndQuestionResponse>(GameErrors.InvalidStateTransition);
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        (GameQuestionSnapshot currentQuestion, List<GameChoiceSnapshot> choices) =
            await QuestionResultsMaterializer.MaterializeAsync(_dbContext, game, utcNow, cancellationToken);
        int totalAnswers = currentQuestion.AcceptedAnswerCount;

        game.Status = GameStatus.QuestionResults;
        game.StateVersion += 1;

        List<QuestionChoiceResultDto> choiceResults = choices
            .Select(c => new QuestionChoiceResultDto(c.Id, c.OrderIndex, c.Text, c.IsCorrect, c.SelectionCount))
            .ToList();

        EndQuestionResponse response = new EndQuestionResponse(
            game.Id,
            "QUESTION_RESULTS",
            game.StateVersion,
            currentQuestion.Id,
            currentQuestion.OrderIndex,
            totalAnswers,
            utcNow,
            choiceResults);

        await _idempotencyService.RecordAsync(
            game.Id,
            hostAccountId,
            request.CommandId,
            nameof(EndQuestionCommand),
            request,
            game.StateVersion,
            response,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Question ended. EventName={EventName} GameId={GameId} StateVersion={StateVersion} QuestionIndex={QuestionIndex} TotalAnswers={TotalAnswers}",
            "QuestionEnded",
            game.Id,
            game.StateVersion,
            currentQuestion.OrderIndex,
            totalAnswers);

        await _notificationService.PublishQuestionEndedAsync(
            hostAccountId,
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

        return Result.Success(response);
    }
}
