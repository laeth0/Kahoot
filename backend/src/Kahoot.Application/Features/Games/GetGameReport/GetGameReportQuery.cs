namespace Kahoot.Application.Features.Games.GetGameReport;

using Kahoot.Application.Common.Messaging;

public sealed record GetGameReportQuery(Guid GameId) : IQuery<GetGameReportResponse>;
