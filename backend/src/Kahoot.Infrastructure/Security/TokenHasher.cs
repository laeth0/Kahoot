using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Interfaces;

namespace Kahoot.Infrastructure.Security;

public sealed class TokenHasher : ITokenHasher, ISingletonService
{
    public string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
