namespace Kahoot.Application.Features.Games.SubmitAnswer;

// Answer Submission Response - Acknowledges answer acceptance and reports whether the submission was an idempotent replay.
public sealed record SubmitAnswerResponse(
    bool Accepted,
    bool AlreadyAnswered);
