namespace Kahoot.Application.Features.Games.AdvanceQuestion;

using Kahoot.Application.Features.Games.Models;

public sealed record AdvanceQuestionResponse(
    Guid GameId,
    string Status,
    long StateVersion,
    CurrentQuestionDto CurrentQuestion);
