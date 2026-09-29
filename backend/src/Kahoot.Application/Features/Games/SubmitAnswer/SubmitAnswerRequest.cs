namespace Kahoot.Application.Features.Games.SubmitAnswer;

// Answer Submission Request Contract - Encapsulates submitted choice selections for active question.
public sealed record SubmitAnswerRequest(
    Guid QuestionId,
    List<Guid> ChoiceIds);
