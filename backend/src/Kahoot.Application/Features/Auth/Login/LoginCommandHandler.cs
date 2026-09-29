using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common;
using Kahoot.Application.Common.Exceptions;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Options;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace Kahoot.Application.Features.Auth.Login;

public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, LoginResult>
{
    private const int RefreshTokenEntropyBytes = 32;

    private readonly IAppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ILoginRateLimiter _loginRateLimiter;
    private readonly IOptions<RefreshTokenOptions> _refreshTokenOptions;
    private readonly TimeProvider _timeProvider;

    public LoginCommandHandler(
        IAppDbContext dbContext,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        ILoginRateLimiter loginRateLimiter,
        IOptions<RefreshTokenOptions> refreshTokenOptions,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _loginRateLimiter = loginRateLimiter;
        _refreshTokenOptions = refreshTokenOptions;
        _timeProvider = timeProvider;
    }

    public async Task<Result<LoginResult>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        // IP Rate Limiting - Throttles high-frequency automated requests from a single IP address (Dimension 1)
        if (_loginRateLimiter.IsIpRateLimited(request.IpAddress))
        {
            return Result.Failure<LoginResult>(AuthErrors.RateLimited);
        }

        // Canonical Identity Resolution - Matches display and invariant uppercase forms to prevent character collision
        string displayUsername = UsernameNormalization.GetDisplayUsername(request.Username);
        string normalizedUsername = UsernameNormalization.GetNormalizedUsername(displayUsername);

        // Progressive Backoff - Exponential delay (1s to 10s) after repeated failures neutralizes credential stuffing without account lockout (Dimension 2)
        TimeSpan backoffDelay = _loginRateLimiter.GetUsernameBackoffDelay(normalizedUsername);
        if (backoffDelay > TimeSpan.Zero)
        {
            await Task.Delay(backoffDelay, _timeProvider, cancellationToken);
        }

        // Query Performance: AsNoTracking() eliminates tracking overhead; SingleOrDefaultAsync performs fast indexed seek on unique NormalizedUsername
        User? user = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.NormalizedUsername == normalizedUsername, cancellationToken);

        // Credential verification under concurrency gate (max 16 active, max 50 queued)
        bool isPasswordValid;
        try
        {
            if (user is null)
            {
                // Timing-Attack Defense - Dummy verification with identical work parameters ensures indistinguishable response time for non-existent users
                await _passwordHasher.VerifyDummyPasswordAsync(request.Password, cancellationToken);
                _loginRateLimiter.RecordFailedAttempt(normalizedUsername);
                return Result.Failure<LoginResult>(AuthErrors.InvalidCredentials);
            }

            isPasswordValid = await _passwordHasher.VerifyPasswordAsync(request.Password, user.PasswordHash, cancellationToken);
        }
        catch (PasswordHashingRateLimitedException)
        {
            return Result.Failure<LoginResult>(AuthErrors.RateLimited);
        }

        if (!isPasswordValid)
        {
            _loginRateLimiter.RecordFailedAttempt(normalizedUsername);
            return Result.Failure<LoginResult>(AuthErrors.InvalidCredentials);
        }

        // Account Enumeration Resistance - Returns generic 401 for inactive/suspended accounts without leaking account status
        if (user.Status != UserStatus.Active)
        {
            _loginRateLimiter.RecordFailedAttempt(normalizedUsername);
            return Result.Failure<LoginResult>(AuthErrors.InvalidCredentials);
        }

        // Security & Concurrency: GetUserForUpdateAsync acquires a pessimistic row lock (SELECT FOR UPDATE) to ensure account credentials/status have not changed concurrently
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        User? currentUser = await _dbContext.GetUserForUpdateAsync(user.Id, cancellationToken);
        if (currentUser is null || currentUser.Status != UserStatus.Active ||
            !string.Equals(currentUser.PasswordHash, user.PasswordHash, StringComparison.Ordinal))
        {
            _loginRateLimiter.RecordFailedAttempt(normalizedUsername);
            return Result.Failure<LoginResult>(AuthErrors.InvalidCredentials);
        }

        // Stateless Access Token - Generates short-lived bearer JWT carrying claims and TokenSecurityVersion
        AccessTokenResult accessToken = _jwtTokenGenerator.GenerateAccessToken(currentUser);

        // Hashed Refresh Token Storage - Issues cryptographically random secret to client and stores only SHA-256 hash in database
        byte[] randomBytes = RandomNumberGenerator.GetBytes(RefreshTokenEntropyBytes);
        string rawRefreshToken = Base64Url.EncodeToString(randomBytes);
        byte[] tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(rawRefreshToken));

        RefreshTokenOptions refreshTokenOptions = _refreshTokenOptions.Value;
        DateTimeOffset now = _timeProvider.GetUtcNow();
        RefreshToken refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = currentUser.Id,
            TokenFamilyId = Guid.NewGuid(),
            FamilyCreatedAt = now,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now.AddDays(refreshTokenOptions.LifetimeDays),
            RotatedAt = null,
            RevokedAt = null
        };

        // Session Persistence: Inserts high-entropy refresh token hash under active row lock
        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _loginRateLimiter.ResetFailedAttempts(normalizedUsername);

        LoginResponse response = new LoginResponse(
            currentUser.Id,
            currentUser.DisplayUsername,
            currentUser.Role.ToString(),
            accessToken.Token,
            accessToken.ExpiresInSeconds);

        return Result.Success(new LoginResult(response, rawRefreshToken));
    }
}
