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
        // 1. Enforce IP rate limiting (30 login attempts/minute per IP)
        if (_loginRateLimiter.IsIpRateLimited(request.IpAddress))
        {
            return Result.Failure<LoginResult>(AuthErrors.RateLimited);
        }

        // 2. Canonical username normalization (NFKC + invariant uppercase, identical to registration)
        string displayUsername = UsernameNormalization.GetDisplayUsername(request.Username);
        string normalizedUsername = UsernameNormalization.GetNormalizedUsername(displayUsername);

        // 3. Progressive username backoff delay after 5 failed attempts (1s, 2s, 4s, 8s, max 10s)
        TimeSpan backoffDelay = _loginRateLimiter.GetUsernameBackoffDelay(normalizedUsername);
        if (backoffDelay > TimeSpan.Zero)
        {
            await Task.Delay(backoffDelay, _timeProvider, cancellationToken);
        }

        // 4. Lookup user globally across Host and System Administrator accounts
        User? user = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.NormalizedUsername == normalizedUsername, cancellationToken);

        // 5. Credential verification under AUTH-HASH-002 gate (max 16 active, max 50 queued)
        bool isPasswordValid;
        try
        {
            if (user is null)
            {
                // Execute dummy verification with identical parameters to defend against timing enumeration
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

        // 6. Suspended accounts return generic 401 without leaking existence or account status
        if (user.Status != UserStatus.Active)
        {
            _loginRateLimiter.RecordFailedAttempt(normalizedUsername);
            return Result.Failure<LoginResult>(AuthErrors.InvalidCredentials);
        }

        // Recheck the account under the same row lock used by password changes,
        // logout-all, and refresh so a stale password cannot create a new session.
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        User? currentUser = await _dbContext.GetUserForUpdateAsync(user.Id, cancellationToken);
        if (currentUser is null || currentUser.Status != UserStatus.Active ||
            !string.Equals(currentUser.PasswordHash, user.PasswordHash, StringComparison.Ordinal))
        {
            _loginRateLimiter.RecordFailedAttempt(normalizedUsername);
            return Result.Failure<LoginResult>(AuthErrors.InvalidCredentials);
        }

        AccessTokenResult accessToken = _jwtTokenGenerator.GenerateAccessToken(currentUser);

        // Persist only SHA256(rawToken).
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
