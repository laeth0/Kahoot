using System.Security.Cryptography;
using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Interfaces;

namespace Kahoot.Infrastructure.Security;

public sealed class SecureTokenGenerator : ISecureTokenGenerator, ISingletonService
{
    private const int TokenByteLength = 32;

    public string GenerateToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
