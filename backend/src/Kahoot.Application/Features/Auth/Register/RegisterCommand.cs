using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Auth.Register;

public sealed record RegisterCommand(string Username, string Password) : ICommand<RegisterResponse>;
