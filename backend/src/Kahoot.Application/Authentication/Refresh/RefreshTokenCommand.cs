using Kahoot.Application.Authentication.Common;
using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Authentication.Refresh;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<AuthenticationResponse>;
