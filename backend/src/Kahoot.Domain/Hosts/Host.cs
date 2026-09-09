using Kahoot.Domain.Common;
using Kahoot.Domain.Quizzes;

namespace Kahoot.Domain.Hosts;

public sealed class Host : AuditableEntity
{
    private readonly List<Quiz> _quizzes = [];
    private readonly List<RefreshToken> _refreshTokens = [];

    private Host()
    {
    }

    private Host(string email, string displayName, string passwordHash)
    {
        Email = email;
        DisplayName = displayName;
        PasswordHash = passwordHash;
    }

    public string Email { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public IReadOnlyCollection<Quiz> Quizzes => _quizzes.AsReadOnly();

    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    public static Host Register(string email, string displayName, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        }

        return new Host(email.Trim().ToLowerInvariant(), displayName.Trim(), passwordHash);
    }

    public void ChangePassword(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        }

        PasswordHash = passwordHash;
    }
}
