namespace Kahoot.Application.Features.Games.RemoveParticipant;

using Kahoot.Application.Common.Messaging;

public sealed record RemoveParticipantCommand(
    Guid GameId,
    Guid ParticipantId) : ICommand;
