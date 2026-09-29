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

    // Memory-Hard Cryptographic Hashing (Argon2id) - 64 MiB RAM per calculation resists massively parallel GPU/ASIC cracking
    private const int MemorySizeKiB = 65_536; // 64 MiB
    private const int Iterations = 3;
    private const int Parallelism = 1;
    private static readonly string ExpectedParameters = $"m={MemorySizeKiB},t={Iterations},p={Parallelism}";
    private const int SaltSize = 16;
    private const int HashSize = 32;

    // Concurrency Gating & Memory Cap (Anti-DoS) - Limits active hashing to 16 (1 GiB ceiling) and queues 50 to prevent OOM crash
    private const int MaxActiveHashingOperations = 16;
    private const int MaxQueuedHashingOperations = 50;

    private readonly SemaphoreSlim _gate = new(MaxActiveHashingOperations, MaxActiveHashingOperations);
    private int _queuedCount;

    // Dummy-Hash Verification - Genuine precomputed hash ensures dummy check performs identical CPU/RAM work to defeat timing side-channels
    private const string PrecomputedDummyHash =
        "$argon2id$v=19$m=65536,t=3,p=1$cycT0VRAQ5f2pQHSVXARzQ==$pZ8wc4Uvqt1H5tUzQhNtbjxA5d/BCukzj/lNemTYOoY=";

    public async Task<string> HashPasswordAsync(string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(password);

        using IDisposable lease = await EnterGateAsync(cancellationToken);

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

        string[] parts = passwordHash.Split('$');
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

        using IDisposable lease = await EnterGateAsync(cancellationToken);

        byte[] actualHash = HashWithArgon2id(password, salt);
        // Constant-Time Comparison - CryptographicOperations.FixedTimeEquals compares all bytes in constant time to prevent timing leak side-channels
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    public Task<bool> VerifyDummyPasswordAsync(string password, CancellationToken cancellationToken = default)
    {
        return VerifyPasswordAsync(password, PrecomputedDummyHash, cancellationToken);
    }

    private async Task<IDisposable> EnterGateAsync(CancellationToken cancellationToken)
    {
        // Fast path: a slot is immediately available — acquire without queuing.
        if (_gate.Wait(0))
        {
            return new GateReleaser(_gate);
        }

        // Slow path: all active slots are taken; join the queue.
        if (Interlocked.Increment(ref _queuedCount) > MaxQueuedHashingOperations)
        {
            // Queue is full — undo increment and surface 429.
            Interlocked.Decrement(ref _queuedCount);
            throw new PasswordHashingRateLimitedException();
        }

        // Wait for a slot; always decrement the queue counter on exit, whether
        // we succeeded, were cancelled, or the wait threw for any other reason.
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

    private static byte[] HashWithArgon2id(string password, byte[] salt)
    {
        using Argon2id argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
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
            // Interlocked ensures Release is called at most once even if Dispose
            // is called concurrently or more than once (e.g. double-using).
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _gate.Release();
            }
        }
    }
}
