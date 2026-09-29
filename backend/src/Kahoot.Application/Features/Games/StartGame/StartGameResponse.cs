namespace Kahoot.Application.Features.Games.StartGame;

using Kahoot.Application.Features.Games.Models;

public sealed record StartGameResponse(
    Guid GameId,
    string Status,
    long StateVersion,
    CurrentQuestionDto CurrentQuestion);
