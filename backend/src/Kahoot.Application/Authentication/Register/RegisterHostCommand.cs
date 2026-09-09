using Kahoot.Application.Authentication.Common;
using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Authentication.Register;

public sealed record RegisterHostCommand(string Username, string Password) : ICommand<AuthenticationResponse>;
