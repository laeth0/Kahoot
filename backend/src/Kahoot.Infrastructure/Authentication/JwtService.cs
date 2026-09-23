using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common.Authentication;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Results;
using Kahoot.Domain.Entities;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Kahoot.Infrastructure.Authentication;

internal sealed class JwtService : IJwtService, IScopedService
{
    private const int RefreshTokenByteCount = 32;
    private const int RefreshTokenLength = 43;

    private static readonly Error InvalidRefreshToken =
        Error.Unauthorized("Auth.InvalidRefreshToken", "The refresh token is invalid or expired.");

    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly JwtOptions _options;
    private readonly SigningCredentials _signingCredentials;

    public JwtService(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        IOptions<JwtOptions> options)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _options = options.Value;
        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Convert.FromHexString(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public async Task<Result<TokenPair>> IssueAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<TokenPair>(
                Error.NotFound("Auth.UserNotFound", "The user was not found."));
        }

        var (pair, refreshToken) = CreateTokens(user, _timeProvider.GetUtcNow());
        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(pair);
    }

    public async Task<Result<TokenPair>> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (refreshToken is null || refreshToken.Length != RefreshTokenLength)
        {
            return Result.Failure<TokenPair>(InvalidRefreshToken);
        }

        var now = _timeProvider.GetUtcNow();
        var tokenHash = HashToken(refreshToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var currentToken = await _dbContext.RefreshTokens.AsNoTracking()
            .SingleOrDefaultAsync(
                token => token.TokenHash == tokenHash
                    && token.RevokedAt == null
                    && token.ExpiresAt > now,
                cancellationToken);

        if (currentToken is null)
        {
            return Result.Failure<TokenPair>(InvalidRefreshToken);
        }

        var user = await _dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == currentToken.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<TokenPair>(InvalidRefreshToken);
        }

        var updated = await _dbContext.RefreshTokens
            .Where(token => token.Id == currentToken.Id
                && token.RevokedAt == null
                && token.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAt, now),
                cancellationToken);

        if (updated != 1)
        {
            return Result.Failure<TokenPair>(InvalidRefreshToken);
        }

        var (pair, newRefreshToken) = CreateTokens(user, now);
        _dbContext.RefreshTokens.Add(newRefreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(pair);
    }

    public async Task<bool> RevokeAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (refreshToken is null || refreshToken.Length != RefreshTokenLength)
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();
        var tokenHash = HashToken(refreshToken);

        var updated = await _dbContext.RefreshTokens
            .Where(token => token.TokenHash == tokenHash
                && token.RevokedAt == null
                && token.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAt, now),
                cancellationToken);

        return updated == 1;
    }

    private (TokenPair Pair, RefreshToken RefreshToken) CreateTokens(User user, DateTimeOffset now)
    {
        var accessTokenExpiresAt = now.AddMinutes(_options.AccessTokenMinutes);
        var refreshTokenExpiresAt = now.AddDays(_options.RefreshTokenDays);
        var rawRefreshToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(RefreshTokenByteCount));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString("D")),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var accessToken = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: accessTokenExpiresAt.UtcDateTime,
            signingCredentials: _signingCredentials);

        var pair = new TokenPair(
            new JwtSecurityTokenHandler().WriteToken(accessToken),
            rawRefreshToken,
            accessTokenExpiresAt,
            refreshTokenExpiresAt);

        var storedRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(rawRefreshToken),
            CreatedAt = now,
            ExpiresAt = refreshTokenExpiresAt
        };

        return (pair, storedRefreshToken);
    }

    private static string HashToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
