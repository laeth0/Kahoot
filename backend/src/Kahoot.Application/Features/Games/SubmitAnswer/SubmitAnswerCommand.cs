namespace Kahoot.Application.Features.Games.SubmitAnswer;

using Kahoot.Application.Common.Messaging;

// Submit Answer Command - Transmits choice selections from an authenticated participant under authoritative timing and rate limits.
public sealed record SubmitAnswerCommand(
    Guid GameId,
    Guid ParticipantId,
    Guid QuestionId,
    List<Guid> ChoiceIds,
    string ConnectionId,
    byte[]? SessionTokenHash,
    long? ConnectionGeneration) : ICommand<SubmitAnswerResponse>;
