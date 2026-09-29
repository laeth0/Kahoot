namespace Kahoot.Application.Features.Games.GetJoinInfo;

using Kahoot.Application.Common.Messaging;

public sealed record GetJoinInfoQuery(string Pin) : IQuery<GetJoinInfoResponse>;
