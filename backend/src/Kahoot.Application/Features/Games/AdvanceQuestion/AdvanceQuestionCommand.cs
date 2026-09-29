namespace Kahoot.Application.Features.Games.AdvanceQuestion;

using Kahoot.Application.Common.Messaging;

public sealed record AdvanceQuestionCommand(
    Guid GameId,
    Guid CommandId,
    long ExpectedStateVersion) : ICommand<AdvanceQuestionResponse>;
