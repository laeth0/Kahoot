using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Games.Presence;

public sealed record AuthorizeHostGameQuery(Guid GameId, Guid HostId) : IQuery<bool>;
