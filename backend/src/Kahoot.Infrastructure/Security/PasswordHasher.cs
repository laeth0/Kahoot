using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common.Exceptions;
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

    // AUTH-HASH-002: max 16 active concurrent hashing operations, max 50 queued.
    private const int MaxActiveHashingOperations = 16;
    private const int MaxQueuedHashingOperations = 50;

    private readonly SemaphoreSlim _gate = new(MaxActiveHashingOperations, MaxActiveHashingOperations);
    private int _queuedCount;

    // Canonical dummy hash generated using identical work parameters to defend against timing enumeration attacks.
    private static readonly string PrecomputedDummyHash =
        $"${ExpectedAlgorithm}${ExpectedVersion}${ExpectedParameters}${Convert.ToBase64String(new byte[SaltSize])}${Convert.ToBase64String(new byte[HashSize])}";

    public async Task<string> HashPasswordAsync(string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(password);

        using var lease = await EnterGateAsync(cancellationToken);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = HashWithArgon2id(password, salt);

        return $"${ExpectedAlgorithm}${ExpectedVersion}${ExpectedParameters}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public async Task<bool> VerifyPasswordAsync(string password, string passwordHash, CancellationToken cancellationToken = default)
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

        if (!string.Equals(parts[1], ExpectedAlgorithm, StringComparison.Ordinal) ||
            !string.Equals(parts[2], ExpectedVersion, StringComparison.Ordinal) ||
            !string.Equals(parts[3], ExpectedParameters, StringComparison.Ordinal))
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

        using var lease = await EnterGateAsync(cancellationToken);

        byte[] actualHash = HashWithArgon2id(password, salt);
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    public Task<bool> VerifyDummyPasswordAsync(string password, CancellationToken cancellationToken = default)
    {
        return VerifyPasswordAsync(password, PrecomputedDummyHash, cancellationToken);
    }

    public string HashPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        using var lease = EnterGateSync();

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

        if (!string.Equals(parts[1], ExpectedAlgorithm, StringComparison.Ordinal) ||
            !string.Equals(parts[2], ExpectedVersion, StringComparison.Ordinal) ||
            !string.Equals(parts[3], ExpectedParameters, StringComparison.Ordinal))
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

        using var lease = EnterGateSync();

        byte[] actualHash = HashWithArgon2id(password, salt);
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    private async Task<IDisposable> EnterGateAsync(CancellationToken cancellationToken)
    {
        if (_gate.Wait(0))
        {
            return new GateReleaser(_gate);
        }

        if (Interlocked.Increment(ref _queuedCount) > MaxQueuedHashingOperations)
        {
            Interlocked.Decrement(ref _queuedCount);
            throw new PasswordHashingRateLimitedException();
        }

        try
        {
            await _gate.WaitAsync(cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref _queuedCount);
        }

        return new GateReleaser(_gate);
    }

    private IDisposable EnterGateSync()
    {
        if (_gate.Wait(0))
        {
            return new GateReleaser(_gate);
        }

        if (Interlocked.Increment(ref _queuedCount) > MaxQueuedHashingOperations)
        {
            Interlocked.Decrement(ref _queuedCount);
            throw new PasswordHashingRateLimitedException();
        }

        try
        {
            _gate.Wait();
        }
        finally
        {
            Interlocked.Decrement(ref _queuedCount);
        }

        return new GateReleaser(_gate);
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

    private sealed class GateReleaser : IDisposable
    {
        private readonly SemaphoreSlim _gate;
        private int _disposed;

        public GateReleaser(SemaphoreSlim gate)
        {
            _gate = gate;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _gate.Release();
            }
        }
    }
}
