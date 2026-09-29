namespace Kahoot.Application.Features.Games.CreateGame;

using Kahoot.Application.Common.Exceptions;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Options;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Quizzes;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class CreateGameCommandHandler : ICommandHandler<CreateGameCommand, CreateGameResponse>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IPinGeneratorService _pinGeneratorService;
    private readonly TimeProvider _timeProvider;
    private readonly GameJoinOptions _joinOptions;
    private readonly ILogger<CreateGameCommandHandler> _logger;

    public CreateGameCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        IPinGeneratorService pinGeneratorService,
        TimeProvider timeProvider,
        IOptions<GameJoinOptions> joinOptions,
        ILogger<CreateGameCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _pinGeneratorService = pinGeneratorService;
        _timeProvider = timeProvider;
        _joinOptions = joinOptions.Value;
        _logger = logger;
    }

    public async Task<Result<CreateGameResponse>> Handle(
        CreateGameCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<CreateGameResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        // Transactional Atomicity: Guarantees game, question snapshots, and choice snapshots commit together or roll back entirely
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        // Concurrency & Pessimistic Row Lock: GetUserForUpdateAsync acquires SELECT FOR UPDATE on Host row, serializing game creation and quiz edits
        User? host = await _dbContext.GetUserForUpdateAsync(hostAccountId, cancellationToken);
        if (host is null || host.Role != UserRole.Host || host.Status != UserStatus.Active)
        {
            return Result.Failure<CreateGameResponse>(AuthErrors.Unauthorized);
        }

        // Query Performance: AsNoTracking() retrieves quiz template for cloning without tracking overhead
        Quiz? quiz = await _dbContext.Quizzes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                q => q.Id == request.QuizId && q.HostAccountId == hostAccountId,
                cancellationToken);

        if (quiz is null)
        {
            return Result.Failure<CreateGameResponse>(QuizErrors.NotFound);
        }

        // System Design & Immutable Snapshotting: Clones template questions into independent GameQuestionSnapshot entities, isolating live games from future quiz edits
        List<Question> questions = await _dbContext.Questions
            .Include(question => question.Image)
            .AsNoTracking()
            .Where(question => question.QuizId == quiz.Id && question.HostAccountId == hostAccountId)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync(cancellationToken);

        if (questions.Count == 0)
        {
            return Result.Failure<CreateGameResponse>(Error.Validation(
                "Validation.Failed", "The quiz must contain at least one question to create a game."));
        }

        List<Guid> questionIds = questions.Select(question => question.Id).ToList();

        // Query Performance: AsNoTracking() and Contains() batch-load all choices across questions in a single indexed query
        List<Choice> choices = await _dbContext.Choices
            .AsNoTracking()
            .Where(choice => questionIds.Contains(choice.QuestionId) && choice.HostAccountId == hostAccountId)
            .OrderBy(choice => choice.OrderIndex)
            .ToListAsync(cancellationToken);

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        Guid gameId = Guid.NewGuid();

        Game game = new Game
        {
            Id = gameId,
            HostAccountId = hostAccountId,
            SourceQuizId = quiz.Id,
            Title = quiz.Title,
            Pin = _pinGeneratorService.GeneratePin(),
            Status = GameStatus.Lobby,
            StateVersion = 1,
            PresenceVersion = 0,
            ReservedParticipantCount = 0,
            NextSeatNumber = 1,
            CurrentQuestionIndex = null,
            HostGraceExpiresAt = utcNow.AddSeconds(300),
            IsTerminatedBySuspension = false,
            CreatedAt = utcNow,
            FinishedAt = null
        };

        _dbContext.Games.Add(game);

        Dictionary<Guid, List<Choice>> choicesByQuestionId = choices
            .GroupBy(choice => choice.QuestionId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (Question question in questions)
        {
            Guid gameQuestionId = Guid.NewGuid();
            string? imageUrl = question.Image?.StoragePath;

            GameQuestionSnapshot questionSnapshot = new GameQuestionSnapshot
            {
                Id = gameQuestionId,
                HostAccountId = hostAccountId,
                GameId = gameId,
                OrderIndex = question.OrderIndex + 1,
                Text = question.Text,
                ImageId = question.ImageId,
                ImageUrl = imageUrl,
                DurationSeconds = question.DurationSeconds,
                BasePoints = question.BasePoints,
                StartedAt = null,
                EndsAt = null,
                InitialEligibleParticipantCount = 0,
                EffectiveEligibleParticipantCount = 0,
                AcceptedAnswerCount = 0,
                ResultsMaterializedAt = null
            };

            _dbContext.GameQuestionSnapshots.Add(questionSnapshot);

            if (choicesByQuestionId.TryGetValue(question.Id, out List<Choice>? questionChoices))
            {
                foreach (Choice choice in questionChoices)
                {
                    GameChoiceSnapshot choiceSnapshot = new GameChoiceSnapshot
                    {
                        Id = Guid.NewGuid(),
                        HostAccountId = hostAccountId,
                        GameQuestionId = gameQuestionId,
                        OrderIndex = choice.OrderIndex + 1,
                        Text = choice.Text,
                        IsCorrect = choice.IsCorrect,
                        SelectionCount = 0
                    };

                    _dbContext.GameChoiceSnapshots.Add(choiceSnapshot);
                }
            }
        }

        // System Design & Partial Unique Index: Retries up to 5 times on PIN collision against partial index ux_games_active_pin (WHERE status <> 'FINISHED')
        const int maxPinAttempts = 5;
        for (int attempt = 1; attempt <= maxPinAttempts; attempt++)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                break;
            }
            catch (UniqueConstraintViolationException exception)
                when (exception.ConstraintName == "ux_games_active_pin")
            {
                if (attempt == maxPinAttempts)
                {
                    return Result.Failure<CreateGameResponse>(GameErrors.PinUnavailable);
                }

                game.Pin = _pinGeneratorService.GeneratePin();
            }
        }

        _logger.LogInformation(
            "Game session created. EventName={EventName} GameId={GameId} HostAccountId={HostAccountId} TotalQuestions={TotalQuestions}",
            "GameSessionCreated",
            game.Id,
            hostAccountId,
            questions.Count);

        Uri baseUri = new Uri(_joinOptions.ClientBaseUrl.TrimEnd('/') + "/");
        string joinUrl = new Uri(baseUri, $"join/{game.Pin}").AbsoluteUri;

        CreateGameResponse response = new CreateGameResponse(
            game.Id,
            game.Pin!,
            joinUrl,
            game.Title,
            "LOBBY",
            game.StateVersion,
            questions.Count,
            game.CreatedAt);

        return Result.Success(response);
    }
}
