using Kahoot.Application.Common.Exceptions;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Features.Auth.Register;

public sealed class RegisterCommandHandler : ICommandHandler<RegisterCommand, RegisterResponse>
{
    private const string NormalizedUsernameConstraintName = "ux_users_normalized_username";

    private readonly IAppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _timeProvider;

    public RegisterCommandHandler(
        IAppDbContext dbContext,
        IPasswordHasher passwordHasher,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
    }

    public async Task<Result<RegisterResponse>> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken)
    {
        var displayUsername = UsernameNormalization.GetDisplayUsername(request.Username);
        var normalizedUsername = UsernameNormalization.GetNormalizedUsername(displayUsername);

        var usernameExists = await _dbContext.Users
            .AnyAsync(user => user.NormalizedUsername == normalizedUsername, cancellationToken);

        if (usernameExists)
        {
            return Result.Failure<RegisterResponse>(AuthErrors.UsernameUnavailable);
        }

        var utcNow = _timeProvider.GetUtcNow();
        string passwordHash;
        try
        {
            passwordHash = await _passwordHasher.HashPasswordAsync(request.Password, cancellationToken);
        }
        catch (PasswordHashingRateLimitedException)
        {
            return Result.Failure<RegisterResponse>(AuthErrors.RateLimited);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayUsername = displayUsername,
            NormalizedUsername = normalizedUsername,
            PasswordHash = passwordHash,
            Role = UserRole.Host,
            Status = UserStatus.Active,
            TokenSecurityVersion = 1,
            Revision = 1,
            TerminationPending = false,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        _dbContext.Users.Add(user);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (string.Equals(ex.ConstraintName, NormalizedUsernameConstraintName, StringComparison.Ordinal))
        {
            return Result.Failure<RegisterResponse>(AuthErrors.UsernameUnavailable);
        }

        return Result.Success(new RegisterResponse(user.Id, user.DisplayUsername));
    }
}
