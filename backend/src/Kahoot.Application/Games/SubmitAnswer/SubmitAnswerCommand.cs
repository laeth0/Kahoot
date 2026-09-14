using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Games.Common;

namespace Kahoot.Application.Games.SubmitAnswer;

public sealed record SubmitAnswerCommand(
    Guid GameId,
    Guid QuestionId,
    string ParticipantSessionToken,
    IReadOnlyList<Guid> SelectedChoiceIds) : ICommand<AnswerAckResponse>;
