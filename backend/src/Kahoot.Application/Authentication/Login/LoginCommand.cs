using Kahoot.Application.Authentication.Common;
using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Authentication.Login;

public sealed record LoginCommand(string Username, string Password) : ICommand<AuthenticationResponse>;
