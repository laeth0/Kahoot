namespace Kahoot.Application.Features.Games.GetGame;

using Kahoot.Application.Common.Messaging;

public sealed record GetGameQuery(Guid GameId) : IQuery<GetGameResponse>;
