using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Games.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.Reconnect;

internal sealed class ReconnectParticipantCommandHandler(ITokenHasher tokenHasher, IApplicationDbContext dbContext)
    : ICommandHandler<ReconnectParticipantCommand, PlayerGameStateResponse>
{
    public async Task<Result<PlayerGameStateResponse>> Handle(
        ReconnectParticipantCommand command,
        CancellationToken cancellationToken)
    {
        string tokenHash = tokenHasher.Hash(command.SessionToken);

        var participant = await dbContext.Participants
            .AsNoTracking()
            .Where(candidate => candidate.SessionTokenHash == tokenHash)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Nickname,
                candidate.TotalScore,
                candidate.LastRank,
                candidate.IsRemoved,
                GameId = candidate.GameSession!.Id,
                candidate.GameSession.Status,
                candidate.GameSession.CurrentQuestionId,
                candidate.GameSession.CurrentQuestionIndex,
                candidate.GameSession.CurrentQuestionEndsAt,
                candidate.GameSession.QuizId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (participant is null)
        {
            return Result.Failure<PlayerGameStateResponse>(GameErrors.InvalidSessionToken);
        }

        if (participant.IsRemoved)
        {
            return Result.Failure<PlayerGameStateResponse>(GameErrors.ParticipantRemoved);
        }

        PlayerQuestionResponse? currentQuestion = null;
        bool alreadyAnswered = false;

        if (participant.Status == GameStatus.QuestionActive && participant.CurrentQuestionId is { } questionId)
        {
            int totalQuestions = await dbContext.Questions
                .CountAsync(question => question.QuizId == participant.QuizId, cancellationToken);

            var question = await dbContext.Questions
                .AsNoTracking()
                .Where(candidate => candidate.Id == questionId)
                .Select(candidate => new
                {
                    candidate.Text,
                    candidate.ImageUrl,
                    candidate.TimeLimitSeconds,
                    Choices = candidate.Choices
                        .OrderBy(choice => choice.OrderIndex)
                        .Select(choice => new PlayerChoiceResponse(choice.Id, choice.OrderIndex, choice.Text, choice.ImageUrl))
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (question is not null)
            {
                currentQuestion = new PlayerQuestionResponse(
                    questionId,
                    participant.CurrentQuestionIndex ?? 0,
                    totalQuestions,
                    question.Text,
                    question.ImageUrl,
                    question.TimeLimitSeconds,
                    (participant.CurrentQuestionEndsAt ?? default).ToUtcOffset(),
                    question.Choices);
            }

            alreadyAnswered = await dbContext.Answers.AnyAsync(
                answer => answer.GameSessionId == participant.GameId
                          && answer.QuestionId == questionId
                          && answer.ParticipantId == participant.Id,
                cancellationToken);
        }

        return Result.Success(new PlayerGameStateResponse(
            participant.GameId,
            participant.Status,
            participant.Id,
            participant.Nickname,
            participant.TotalScore,
            participant.LastRank,
            alreadyAnswered,
            currentQuestion));
    }
}
