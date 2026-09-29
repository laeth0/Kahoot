namespace Kahoot.Application.Features.Games.AdvanceQuestion;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Games.Models;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

public sealed class AdvanceQuestionCommandHandler : ICommandHandler<AdvanceQuestionCommand, AdvanceQuestionResponse>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IGameCommandIdempotencyService _idempotencyService;
    private readonly IGameNotificationService _notificationService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AdvanceQuestionCommandHandler> _logger;

    public AdvanceQuestionCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        IGameCommandIdempotencyService idempotencyService,
        IGameNotificationService notificationService,
        TimeProvider timeProvider,
        ILogger<AdvanceQuestionCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _idempotencyService = idempotencyService;
        _notificationService = notificationService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<AdvanceQuestionResponse>> Handle(
        AdvanceQuestionCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<AdvanceQuestionResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        User? host = await _dbContext.GetUserForUpdateAsync(hostAccountId, cancellationToken);
        if (host is null || host.Role != UserRole.Host || host.Status != UserStatus.Active)
        {
            return Result.Failure<AdvanceQuestionResponse>(AuthErrors.Unauthorized);
        }

        Game? game = await _dbContext.GetGameForUpdateAsync(request.GameId, hostAccountId, cancellationToken);
        if (game is null)
        {
            return Result.Failure<AdvanceQuestionResponse>(GameErrors.NotFound);
        }

        IdempotencyCheckResult<AdvanceQuestionResponse> idempotencyResult = await _idempotencyService.CheckAsync<AdvanceQuestionResponse>(
            request.GameId,
            request.CommandId,
            nameof(AdvanceQuestionCommand),
            request,
            cancellationToken);

        if (idempotencyResult.IsReplay)
        {
            if (idempotencyResult.Error is not null)
            {
                return Result.Failure<AdvanceQuestionResponse>(idempotencyResult.Error);
            }

            return Result.Success(idempotencyResult.CachedResponse!);
        }

        if (game.Status == GameStatus.Finished)
        {
            return Result.Failure<AdvanceQuestionResponse>(GameErrors.InvalidStateTransition);
        }

        if (game.HostGraceExpiresAt <= _timeProvider.GetUtcNow())
        {
            return Result.Failure<AdvanceQuestionResponse>(GameErrors.InvalidStateTransition);
        }

        if (game.StateVersion != request.ExpectedStateVersion)
        {
            return Result.Failure<AdvanceQuestionResponse>(GameErrors.ConcurrentModification);
        }

        if (game.Status != GameStatus.QuestionResults && game.Status != GameStatus.Leaderboard)
        {
            return Result.Failure<AdvanceQuestionResponse>(GameErrors.InvalidStateTransition);
        }

        int nextIndex = (game.CurrentQuestionIndex ?? 0) + 1;

        GameQuestionSnapshot? nextQuestion = await _dbContext.GameQuestionSnapshots
            .FirstOrDefaultAsync(
                q => q.GameId == game.Id && q.HostAccountId == hostAccountId && q.OrderIndex == nextIndex,
                cancellationToken);

        if (nextQuestion is null)
        {
            return Result.Failure<AdvanceQuestionResponse>(GameErrors.NoMoreQuestions);
        }

        List<GameChoiceSnapshot> choices = await _dbContext.GameChoiceSnapshots
            .Where(c => c.GameQuestionId == nextQuestion.Id && c.HostAccountId == hostAccountId)
            .OrderBy(c => c.OrderIndex)
            .ToListAsync(cancellationToken);

        int activeParticipantCount = await _dbContext.Participants
            .CountAsync(
                p => p.GameId == game.Id && p.HostAccountId == hostAccountId && !p.IsRemoved,
                cancellationToken);

        int totalQuestions = await _dbContext.GameQuestionSnapshots
            .CountAsync(snapshot => snapshot.GameId == game.Id && snapshot.HostAccountId == hostAccountId,
                cancellationToken);

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        DateTimeOffset endsAt = utcNow.AddSeconds(nextQuestion.DurationSeconds);

        nextQuestion.StartedAt = utcNow;
        nextQuestion.EndsAt = endsAt;
        nextQuestion.InitialEligibleParticipantCount = activeParticipantCount;
        nextQuestion.EffectiveEligibleParticipantCount = activeParticipantCount;
        nextQuestion.AcceptedAnswerCount = 0;

        game.CurrentQuestionIndex = nextIndex;
        game.Status = GameStatus.QuestionActive;
        game.StateVersion += 1;

        List<QuestionChoiceDto> hostChoices = choices
            .Select(c => new QuestionChoiceDto(c.Id, c.OrderIndex, c.Text, c.IsCorrect))
            .ToList();

        CurrentQuestionDto currentQuestionForHost = new CurrentQuestionDto(
            nextQuestion.Id,
            nextQuestion.OrderIndex,
            nextQuestion.Text,
            nextQuestion.ImageUrl,
            nextQuestion.DurationSeconds,
            utcNow,
            endsAt,
            activeParticipantCount,
            hostChoices);

        List<PlayerQuestionChoiceDto> playerChoices = choices
            .Select(c => new PlayerQuestionChoiceDto(c.Id, c.OrderIndex, c.Text))
            .ToList();

        PlayerQuestionStartedEvent currentQuestionForPlayers = new PlayerQuestionStartedEvent(
            game.Id,
            game.StateVersion,
            nextQuestion.Id,
            nextQuestion.OrderIndex,
            totalQuestions,
            nextQuestion.Text,
            nextQuestion.ImageUrl,
            nextQuestion.DurationSeconds,
            utcNow,
            endsAt,
            playerChoices);

        HostQuestionStartedEvent currentQuestionEventForHost = new HostQuestionStartedEvent(
            game.Id,
            game.StateVersion,
            nextQuestion.Id,
            nextQuestion.OrderIndex,
            totalQuestions,
            nextQuestion.Text,
            nextQuestion.ImageUrl,
            nextQuestion.DurationSeconds,
            nextQuestion.BasePoints,
            utcNow,
            endsAt,
            activeParticipantCount,
            hostChoices.Where(choice => choice.IsCorrect == true).Select(choice => choice.ChoiceId).ToList(),
            hostChoices);

        AdvanceQuestionResponse response = new AdvanceQuestionResponse(
            game.Id,
            "QUESTION_ACTIVE",
            game.StateVersion,
            currentQuestionForHost);

        await _idempotencyService.RecordAsync(
            game.Id,
            hostAccountId,
            request.CommandId,
            nameof(AdvanceQuestionCommand),
            request,
            game.StateVersion,
            response,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Question advanced. EventName={EventName} GameId={GameId} StateVersion={StateVersion} QuestionIndex={QuestionIndex}",
            "QuestionAdvanced",
            game.Id,
            game.StateVersion,
            nextIndex);

        await _notificationService.PublishQuestionStartedAsync(
            hostAccountId,
            game.Id,
            game.StateVersion,
            currentQuestionForPlayers,
            currentQuestionEventForHost,
            CancellationToken.None);

        return Result.Success(response);
    }
}
