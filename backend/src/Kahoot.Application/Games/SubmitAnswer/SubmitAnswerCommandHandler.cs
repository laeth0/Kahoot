using System.Diagnostics;
using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Observability;
using Kahoot.Application.Games.Common;
using Kahoot.Application.Games.Scoring;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.SubmitAnswer;

internal sealed class SubmitAnswerCommandHandler(
    IApplicationDbContext dbContext,
    ITokenHasher tokenHasher,
    IScoringService scoringService,
    IDbExceptionInterpreter dbExceptionInterpreter,
    TimeProvider timeProvider,
    IKahootTelemetry telemetry) : ICommandHandler<SubmitAnswerCommand, AnswerAckResponse>
{
    public async Task<Result<AnswerAckResponse>> Handle(SubmitAnswerCommand command, CancellationToken cancellationToken)
    {
        long startTimestamp = Stopwatch.GetTimestamp();
        string outcome = "rejected";

        Activity.Current?.SetTag("game.id", command.GameId);
        Activity.Current?.SetTag("question.id", command.QuestionId);

        try
        {
            string tokenHash = tokenHasher.Hash(command.ParticipantSessionToken);

            var snapshot = await dbContext.GameSessions
                .AsNoTracking()
                .Where(session => session.Id == command.GameId)
                .Select(session => new
                {
                    session.Status,
                    session.CurrentQuestionId,
                    session.CurrentQuestionStartedAt,
                    session.CurrentQuestionEndsAt,
                    Participant = session.Participants
                        .Where(participant => participant.SessionTokenHash == tokenHash)
                        .Select(participant => new { participant.Id, participant.IsRemoved })
                        .FirstOrDefault(),
                    Question = session.QuestionSnapshots
                        .Where(question => question.Id == command.QuestionId)
                        .Select(question => new
                        {
                            question.Points,
                            question.TimeLimitSeconds,
                            Choices = question.Choices.Select(choice => new
                            {
                                choice.Id,
                                choice.IsCorrect
                            }).ToList()
                        })
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (snapshot is null)
            {
                return Result.Failure<AnswerAckResponse>(GameErrors.NotFound);
            }

            if (snapshot.Participant is null)
            {
                return Result.Failure<AnswerAckResponse>(GameErrors.InvalidSessionToken);
            }

            Activity.Current?.SetTag("participant.id", snapshot.Participant.Id);

            if (snapshot.Participant.IsRemoved)
            {
                return Result.Failure<AnswerAckResponse>(GameErrors.ParticipantRemoved);
            }

            if (snapshot.Status != GameStatus.QuestionActive)
            {
                return Result.Failure<AnswerAckResponse>(GameErrors.QuestionNotActive);
            }

            if (snapshot.CurrentQuestionId != command.QuestionId
                || snapshot.Question is null
                || snapshot.CurrentQuestionStartedAt is not { } startedAt
                || snapshot.CurrentQuestionEndsAt is not { } endsAt)
            {
                return Result.Failure<AnswerAckResponse>(GameErrors.NotCurrentQuestion);
            }

            List<Guid> submittedChoiceIds = [.. command.SelectedChoiceIds.Distinct()];
            if (submittedChoiceIds.Count == 0)
            {
                return Result.Failure<AnswerAckResponse>(GameErrors.ChoiceNotInQuestion);
            }

            HashSet<Guid> validChoiceIds = [.. snapshot.Question.Choices.Select(choice => choice.Id)];
            if (!submittedChoiceIds.All(validChoiceIds.Contains))
            {
                return Result.Failure<AnswerAckResponse>(GameErrors.ChoiceNotInQuestion);
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            if (now.UtcDateTime > endsAt)
            {
                outcome = "late";
                return Result.Failure<AnswerAckResponse>(GameErrors.QuestionClosed);
            }

            HashSet<Guid> correctChoiceIds = [.. snapshot.Question.Choices.Where(choice => choice.IsCorrect).Select(choice => choice.Id)];
            bool isCorrect = submittedChoiceIds.Count == correctChoiceIds.Count
                && submittedChoiceIds.All(correctChoiceIds.Contains);

            int responseTimeMs = Math.Max(0, (int)(now.UtcDateTime - startedAt).TotalMilliseconds);
            int pointsAwarded = scoringService.CalculateScore(
                isCorrect,
                TimeSpan.FromMilliseconds(responseTimeMs),
                snapshot.Question.TimeLimitSeconds,
                snapshot.Question.Points);

            Answer answer = new()
            {
                GameSessionId = command.GameId,
                QuestionId = command.QuestionId,
                ParticipantId = snapshot.Participant.Id,
                IsCorrect = isCorrect,
                PointsAwarded = pointsAwarded,
                ResponseTimeMs = responseTimeMs,
                SubmittedAt = now.UtcDateTime,
                SelectedChoices = [.. submittedChoiceIds.Select(choiceId => new AnswerSelectedChoice
                {
                    SelectedChoiceId = choiceId
                })]
            };

            dbContext.Answers.Add(answer);

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
                when (dbExceptionInterpreter.IsUniqueViolation(exception, "uq_answer_participant_question"))
            {
                await transaction.RollbackAsync(cancellationToken);
                outcome = "duplicate";
                return Result.Success(new AnswerAckResponse(Accepted: true, AlreadyAnswered: true));
            }

            if (pointsAwarded > 0)
            {
                await dbContext.Participants
                    .Where(participant => participant.Id == snapshot.Participant.Id)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(
                            participant => participant.TotalScore,
                            participant => participant.TotalScore + pointsAwarded),
                        cancellationToken);
            }

            await dbContext.GameSessions
                .Where(session => session.Id == command.GameId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        session => session.CurrentQuestionAnsweredCount,
                        session => session.CurrentQuestionAnsweredCount + 1),
                    cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            outcome = "accepted";
            return Result.Success(new AnswerAckResponse(Accepted: true, AlreadyAnswered: false));
        }
        catch (Exception)
        {
            outcome = "exception";
            throw;
        }
        finally
        {
            double elapsedSeconds = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
            telemetry.RecordAnswer(outcome, elapsedSeconds);
        }
    }
}
