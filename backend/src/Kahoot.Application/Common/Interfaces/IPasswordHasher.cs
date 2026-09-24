namespace Kahoot.Application.Common.Interfaces;

public interface IPasswordHasher
{
    Task<string> HashPasswordAsync(string password, CancellationToken cancellationToken = default);

    Task<bool> VerifyPasswordAsync(string password, string passwordHash, CancellationToken cancellationToken = default);

    Task<bool> VerifyDummyPasswordAsync(string password, CancellationToken cancellationToken = default);

    string HashPassword(string password);

    bool VerifyPassword(string password, string passwordHash);
}
