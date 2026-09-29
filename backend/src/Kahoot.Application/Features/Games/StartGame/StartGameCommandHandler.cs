namespace Kahoot.Application.Features.Games.StartGame;

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

public sealed class StartGameCommandHandler : ICommandHandler<StartGameCommand, StartGameResponse>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IGameCommandIdempotencyService _idempotencyService;
    private readonly IGameNotificationService _notificationService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StartGameCommandHandler> _logger;

    public StartGameCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        IGameCommandIdempotencyService idempotencyService,
        IGameNotificationService notificationService,
        TimeProvider timeProvider,
        ILogger<StartGameCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _idempotencyService = idempotencyService;
        _notificationService = notificationService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<StartGameResponse>> Handle(
        StartGameCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<StartGameResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        User? host = await _dbContext.GetUserForUpdateAsync(hostAccountId, cancellationToken);
        if (host is null || host.Role != UserRole.Host || host.Status != UserStatus.Active)
        {
            return Result.Failure<StartGameResponse>(AuthErrors.Unauthorized);
        }

        Game? game = await _dbContext.GetGameForUpdateAsync(request.GameId, hostAccountId, cancellationToken);
        if (game is null)
        {
            return Result.Failure<StartGameResponse>(GameErrors.NotFound);
        }

        IdempotencyCheckResult<StartGameResponse> idempotencyResult = await _idempotencyService.CheckAsync<StartGameResponse>(
            request.GameId,
            request.CommandId,
            nameof(StartGameCommand),
            request,
            cancellationToken);

        if (idempotencyResult.IsReplay)
        {
            if (idempotencyResult.Error is not null)
            {
                return Result.Failure<StartGameResponse>(idempotencyResult.Error);
            }

            return Result.Success(idempotencyResult.CachedResponse!);
        }

        if (game.Status == GameStatus.Finished)
        {
            return Result.Failure<StartGameResponse>(GameErrors.InvalidStateTransition);
        }

        if (game.HostGraceExpiresAt <= _timeProvider.GetUtcNow())
        {
            return Result.Failure<StartGameResponse>(GameErrors.InvalidStateTransition);
        }

        if (game.StateVersion != request.ExpectedStateVersion)
        {
            return Result.Failure<StartGameResponse>(GameErrors.ConcurrentModification);
        }

        if (game.Status != GameStatus.Lobby)
        {
            return Result.Failure<StartGameResponse>(GameErrors.InvalidStateTransition);
        }

        GameQuestionSnapshot? question1 = await _dbContext.GameQuestionSnapshots
            .FirstOrDefaultAsync(
                q => q.GameId == game.Id && q.HostAccountId == hostAccountId && q.OrderIndex == 1,
                cancellationToken);

        if (question1 is null)
        {
            return Result.Failure<StartGameResponse>(GameErrors.NoMoreQuestions);
        }

        List<GameChoiceSnapshot> choices = await _dbContext.GameChoiceSnapshots
            .Where(c => c.GameQuestionId == question1.Id && c.HostAccountId == hostAccountId)
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
        DateTimeOffset endsAt = utcNow.AddSeconds(question1.DurationSeconds);

        question1.StartedAt = utcNow;
        question1.EndsAt = endsAt;
        question1.InitialEligibleParticipantCount = activeParticipantCount;
        question1.EffectiveEligibleParticipantCount = activeParticipantCount;
        question1.AcceptedAnswerCount = 0;

        game.CurrentQuestionIndex = 1;
        game.Status = GameStatus.QuestionActive;
        game.StateVersion += 1;

        List<QuestionChoiceDto> hostChoices = choices
            .Select(c => new QuestionChoiceDto(c.Id, c.OrderIndex, c.Text, c.IsCorrect))
            .ToList();

        CurrentQuestionDto currentQuestionForHost = new CurrentQuestionDto(
            question1.Id,
            question1.OrderIndex,
            question1.Text,
            question1.ImageUrl,
            question1.DurationSeconds,
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
            question1.Id,
            question1.OrderIndex,
            totalQuestions,
            question1.Text,
            question1.ImageUrl,
            question1.DurationSeconds,
            utcNow,
            endsAt,
            playerChoices);

        HostQuestionStartedEvent currentQuestionEventForHost = new HostQuestionStartedEvent(
            game.Id,
            game.StateVersion,
            question1.Id,
            question1.OrderIndex,
            totalQuestions,
            question1.Text,
            question1.ImageUrl,
            question1.DurationSeconds,
            question1.BasePoints,
            utcNow,
            endsAt,
            activeParticipantCount,
            hostChoices.Where(choice => choice.IsCorrect == true).Select(choice => choice.ChoiceId).ToList(),
            hostChoices);

        StartGameResponse response = new StartGameResponse(
            game.Id,
            "QUESTION_ACTIVE",
            game.StateVersion,
            currentQuestionForHost);

        await _idempotencyService.RecordAsync(
            game.Id,
            hostAccountId,
            request.CommandId,
            nameof(StartGameCommand),
            request,
            game.StateVersion,
            response,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Game started. EventName={EventName} GameId={GameId} StateVersion={StateVersion} QuestionIndex={QuestionIndex}",
            "GameStarted",
            game.Id,
            game.StateVersion,
            1);

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
