using System.Diagnostics;
using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Observability;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Games.Common;
using Kahoot.Application.Quizzes.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Kahoot.Domain.Quizzes;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.CreateGame;

internal sealed class CreateGameCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IGamePinGenerator pinGenerator,
    IDbExceptionInterpreter dbExceptionInterpreter,
    IJoinUrlGenerator joinUrlGenerator,
    IKahootTelemetry telemetry) : ICommandHandler<CreateGameCommand, CreateGameResponse>
{
    private const int MaxPinAttempts = 5;

    public async Task<Result<CreateGameResponse>> Handle(CreateGameCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.HostId is not { } hostId)
        {
            return Result.Failure<CreateGameResponse>(SharedErrors.Unauthorized);
        }

        Quiz? quiz = await dbContext.Quizzes
            .AsNoTracking()
            .Include(candidate => candidate.Questions)
            .ThenInclude(question => question.Choices)
            .FirstOrDefaultAsync(candidate => candidate.Id == command.QuizId && candidate.HostId == hostId, cancellationToken);

        if (quiz is null)
        {
            return Result.Failure<CreateGameResponse>(QuizErrors.NotFound);
        }

        if (!quiz.IsPublished)
        {
            return Result.Failure<CreateGameResponse>(GameErrors.QuizNotPublished);
        }

        for (int attempt = 0; attempt < MaxPinAttempts; attempt++)
        {
            string pin = await pinGenerator.GenerateUniquePinAsync(cancellationToken);

            GameSession game = new()
            {
                QuizId = command.QuizId,
                HostId = hostId,
                QuizTitle = quiz.Title,
                Pin = pin,
                Status = GameStatus.Lobby
            };

            List<Question> orderedQuestions = [.. quiz.Questions.OrderBy(question => question.OrderIndex)];
            for (int questionIndex = 0; questionIndex < orderedQuestions.Count; questionIndex++)
            {
                Question question = orderedQuestions[questionIndex];
                GameQuestionSnapshot questionSnapshot = new()
                {
                    GameSession = game,
                    SourceQuestionId = question.Id,
                    OrderIndex = questionIndex,
                    Text = question.Text,
                    ImageUrl = question.ImageUrl,
                    TimeLimitSeconds = question.TimeLimitSeconds,
                    Points = question.Points
                };

                foreach (Choice choice in question.Choices.OrderBy(c => c.OrderIndex))
                {
                    GameChoiceSnapshot choiceSnapshot = new()
                    {
                        QuestionSnapshot = questionSnapshot,
                        SourceChoiceId = choice.Id,
                        OrderIndex = choice.OrderIndex,
                        Text = choice.Text,
                        IsCorrect = choice.IsCorrect
                    };
                    questionSnapshot.Choices.Add(choiceSnapshot);
                }

                game.QuestionSnapshots.Add(questionSnapshot);
            }

            dbContext.GameSessions.Add(game);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                Activity.Current?.SetTag("game.id", game.Id);
                Activity.Current?.SetTag("quiz.id", command.QuizId);
                telemetry.RecordGameCreated();
                string joinUrl = joinUrlGenerator.GenerateJoinUrl(game.Pin);
                return Result.Success(new CreateGameResponse(game.Id, game.Pin, game.Status, joinUrl));
            }
            catch (DbUpdateException exception)
                when (dbExceptionInterpreter.IsUniqueViolation(exception, "uq_game_session_active_pin"))
            {
                dbContext.ChangeTracker.Clear();
            }
        }

        return Result.Failure<CreateGameResponse>(GameErrors.PinUnavailable);
    }
}
