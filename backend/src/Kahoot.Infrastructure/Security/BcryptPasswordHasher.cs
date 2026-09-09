using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Security;

namespace Kahoot.Infrastructure.Security;

public sealed class BcryptPasswordHasher : IPasswordHasher, ISingletonService
{
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.EnhancedHashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash) =>
        BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash);
}
