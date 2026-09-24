using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Auth.Login;

public sealed record LoginCommand(
    string Username,
    string Password,
    string IpAddress) : ICommand<LoginResult>;
