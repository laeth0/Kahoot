namespace Kahoot.Application.Features.Games;

public sealed record GameControlRequest(Guid CommandId, long ExpectedStateVersion);
