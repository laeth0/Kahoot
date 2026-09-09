namespace Kahoot.Domain.Hosts;

public sealed class RefreshToken
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    private RefreshToken()
    {
    }

    private RefreshToken(Guid hostId, string tokenHash, DateTime expiresAtUtc, DateTime createdAtUtc)
    {
        HostId = hostId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid HostId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public Host? Host { get; private set; }

    public static RefreshToken Issue(Guid hostId, string tokenHash, DateTime expiresAtUtc, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        }

        if (expiresAtUtc <= nowUtc)
        {
            throw new ArgumentException("Expiry must be in the future.", nameof(expiresAtUtc));
        }

        return new RefreshToken(hostId, tokenHash, expiresAtUtc, nowUtc);
    }

    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && nowUtc < ExpiresAtUtc;

    public void Revoke(DateTime nowUtc, Guid? replacedByTokenId = null)
    {
        if (RevokedAtUtc is not null)
        {
            return;
        }

        RevokedAtUtc = nowUtc;
        ReplacedByTokenId = replacedByTokenId;
    }
}
