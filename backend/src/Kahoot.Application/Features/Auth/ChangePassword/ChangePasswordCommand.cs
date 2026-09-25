using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Auth.ChangePassword;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand;
