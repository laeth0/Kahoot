using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common.Interfaces;
using Konscious.Security.Cryptography;

namespace Kahoot.Infrastructure.Security;

public sealed class PasswordHasher : IPasswordHasher
{
    private const string ExpectedAlgorithm = "argon2id";
    private const string ExpectedVersion = "v=19";
    private const int MemorySizeKiB = 65_536; // 64 MiB
    private const int Iterations = 3;
    private const int Parallelism = 1;
    private static readonly string ExpectedParameters = $"m={MemorySizeKiB},t={Iterations},p={Parallelism}";
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string HashPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = HashWithArgon2id(password, salt);

        return $"${ExpectedAlgorithm}${ExpectedVersion}${ExpectedParameters}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (password is null || string.IsNullOrEmpty(passwordHash))
        {
            return false;
        }

        var parts = passwordHash.Split('$');
        if (parts.Length != 6 || parts[0].Length != 0)
        {
            return false;
        }

        if (!string.Equals(parts[1], ExpectedAlgorithm, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(parts[2], ExpectedVersion, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(parts[3], ExpectedParameters, StringComparison.Ordinal))
        {
            return false;
        }

        byte[] salt = new byte[SaltSize];
        if (!Convert.TryFromBase64String(parts[4], salt, out int saltBytesWritten) || saltBytesWritten != SaltSize)
        {
            return false;
        }

        byte[] expectedHash = new byte[HashSize];
        if (!Convert.TryFromBase64String(parts[5], expectedHash, out int hashBytesWritten) || hashBytesWritten != HashSize)
        {
            return false;
        }

        byte[] actualHash = HashWithArgon2id(password, salt);
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    private static byte[] HashWithArgon2id(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = MemorySizeKiB,
            Iterations = Iterations,
            DegreeOfParallelism = Parallelism
        };

        return argon2.GetBytes(HashSize);
    }
}
