using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Authentication.Logout;

public sealed record LogoutCommand(string RefreshToken) : ICommand;
