using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Auth.Logout;

public sealed record LogoutCommand(string? RawRefreshToken) : ICommand;
