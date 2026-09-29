namespace Kahoot.Application.Features.Games.EndQuestion;

using Kahoot.Application.Features.Games.Models;

public sealed record EndQuestionResponse(
    Guid GameId,
    string Status,
    long StateVersion,
    Guid QuestionId,
    int OrderIndex,
    int TotalAnswers,
    DateTimeOffset ResultsMaterializedAt,
    List<QuestionChoiceResultDto> Choices);
