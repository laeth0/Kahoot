using Kahoot.Domain.Common;
using Kahoot.Domain.Quizzes;

namespace Kahoot.Domain.Hosts;

public sealed class Host : AuditableEntity
{
    public string Email { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public ICollection<Quiz> Quizzes { get; set; } = [];

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
