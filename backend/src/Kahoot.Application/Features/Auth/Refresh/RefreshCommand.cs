using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Auth.Refresh;

public sealed record RefreshCommand(string RawRefreshToken) : ICommand<RefreshResult>;
