namespace Kahoot.Application.Common.Interfaces;

public interface IPasswordHasher
{
    // Cryptographic Password Hashing (AUTH-HASH-001) - Computes Argon2id digest with unique 16-byte cryptographically secure salt
    Task<string> HashPasswordAsync(string password, CancellationToken cancellationToken = default);

    // Cryptographic Verification - Verifies plaintext password against Argon2id hash in constant time
    Task<bool> VerifyPasswordAsync(string password, string passwordHash, CancellationToken cancellationToken = default);

    // Timing Attack Mitigation (AUTH-SEC-003) - Computes dummy Argon2id hash on user-not-found to normalize latency
    Task<bool> VerifyDummyPasswordAsync(string password, CancellationToken cancellationToken = default);
}
