namespace Kahoot.Application.Features.Games.EndQuestion;

using Kahoot.Application.Common.Messaging;

public sealed record EndQuestionCommand(
    Guid GameId,
    Guid CommandId,
    long ExpectedStateVersion) : ICommand<EndQuestionResponse>;
