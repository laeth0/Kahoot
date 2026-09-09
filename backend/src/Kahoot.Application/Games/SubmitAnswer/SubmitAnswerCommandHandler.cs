using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
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
    TimeProvider timeProvider) : ICommandHandler<SubmitAnswerCommand, AnswerAckResponse>
{
    public async Task<Result<AnswerAckResponse>> Handle(SubmitAnswerCommand command, CancellationToken cancellationToken)
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
                Question = session.Quiz!.Questions
                    .Where(question => question.Id == command.QuestionId)
                    .Select(question => new
                    {
                        question.Points,
                        question.TimeLimitSeconds,
                        CorrectChoiceId = question.Choices
                            .Where(choice => choice.IsCorrect)
                            .Select(choice => choice.Id)
                            .FirstOrDefault(),
                        SelectedChoiceExists = question.Choices.Any(choice => choice.Id == command.SelectedChoiceId)
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

        if (!snapshot.Question.SelectedChoiceExists)
        {
            return Result.Failure<AnswerAckResponse>(GameErrors.ChoiceNotInQuestion);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (now.UtcDateTime > endsAt)
        {
            return Result.Failure<AnswerAckResponse>(GameErrors.QuestionClosed);
        }

        int responseTimeMs = Math.Max(0, (int)(now.UtcDateTime - startedAt).TotalMilliseconds);
        bool isCorrect = command.SelectedChoiceId == snapshot.Question.CorrectChoiceId;
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
            SelectedChoiceId = command.SelectedChoiceId,
            IsCorrect = isCorrect,
            PointsAwarded = pointsAwarded,
            ResponseTimeMs = responseTimeMs,
            SubmittedAt = now.UtcDateTime
        };

        dbContext.Answers.Add(answer);

        if (pointsAwarded <= 0)
        {
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
                when (dbExceptionInterpreter.IsUniqueViolation(exception, "uq_answer_participant_question"))
            {
                return Result.Success(new AnswerAckResponse(Accepted: true, AlreadyAnswered: true));
            }

            return Result.Success(new AnswerAckResponse(Accepted: true, AlreadyAnswered: false));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (dbExceptionInterpreter.IsUniqueViolation(exception, "uq_answer_participant_question"))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Success(new AnswerAckResponse(Accepted: true, AlreadyAnswered: true));
        }

        await dbContext.Participants
            .Where(participant => participant.Id == snapshot.Participant.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    participant => participant.TotalScore,
                    participant => participant.TotalScore + pointsAwarded),
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result.Success(new AnswerAckResponse(Accepted: true, AlreadyAnswered: false));
    }
}
