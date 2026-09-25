using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common.Exceptions;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Features.Auth.Login;

public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, LoginResult>
{
    private const int AccessTokenLifetimeSeconds = 900;
    private const int RefreshTokenLifetimeDays = 14;
    private const int RefreshTokenEntropyBytes = 32;

    private readonly IAppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ILoginRateLimiter _loginRateLimiter;
    private readonly TimeProvider _timeProvider;

    public LoginCommandHandler(
        IAppDbContext dbContext,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        ILoginRateLimiter loginRateLimiter,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _loginRateLimiter = loginRateLimiter;
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
        var displayUsername = request.Username.Trim();
        var normalizedUsername = displayUsername.Normalize(NormalizationForm.FormKC).ToUpperInvariant();

        // 3. Progressive username backoff delay after 5 failed attempts (1s, 2s, 4s, 8s, max 10s)
        var backoffDelay = _loginRateLimiter.GetUsernameBackoffDelay(normalizedUsername);
        if (backoffDelay > TimeSpan.Zero)
        {
            await Task.Delay(backoffDelay, _timeProvider, cancellationToken);
        }

        // 4. Lookup user globally across Host and System Administrator accounts
        var user = await _dbContext.Users
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

        // 7. Successful login resets consecutive failed attempts for this username
        _loginRateLimiter.ResetFailedAttempts(normalizedUsername);

        // 8. Generate 15-minute JWT access token
        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user);

        // 9. Generate cryptographically secure refresh token; persist only SHA256(rawToken)
        byte[] randomBytes = RandomNumberGenerator.GetBytes(RefreshTokenEntropyBytes);
        string rawRefreshToken = Base64Url.EncodeToString(randomBytes);
        byte[] tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(rawRefreshToken));

        var now = _timeProvider.GetUtcNow();
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenFamilyId = Guid.NewGuid(),
            FamilyCreatedAt = now,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now.AddDays(RefreshTokenLifetimeDays),
            RotatedAt = null,
            RevokedAt = null
        };

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new LoginResponse(
            user.Id,
            user.DisplayUsername,
            user.Role.ToString(),
            accessToken,
            AccessTokenLifetimeSeconds);

        return Result.Success(new LoginResult(response, rawRefreshToken));
    }
}
