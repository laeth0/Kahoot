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

    public RegisterCommandHandler(
        IAppDbContext dbContext,
        IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<RegisterResponse>> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken)
    {
        // Dual Identity & Unicode Normalization - Generates display form and canonical NFKC representation for DB uniqueness
        string displayUsername = UsernameNormalization.GetDisplayUsername(request.Username);
        string normalizedUsername = UsernameNormalization.GetNormalizedUsername(displayUsername);

        // Performance & Resource Protection: AnyAsync performs an indexed EXISTS query without loading entities into memory, skipping expensive hashing if username is taken
        bool usernameExists = await _dbContext.Users
            .AnyAsync(user => user.NormalizedUsername == normalizedUsername, cancellationToken);

        if (usernameExists)
        {
            return Result.Failure<RegisterResponse>(AuthErrors.UsernameUnavailable);
        }

        // Memory-Hard Hashing (Argon2id) & Anti-DoS - Concurrency-gated hashing consumes 64 MiB RAM per call
        string passwordHash;
        try
        {
            passwordHash = await _passwordHasher.HashPasswordAsync(request.Password, cancellationToken);
        }
        catch (PasswordHashingRateLimitedException)
        {
            return Result.Failure<RegisterResponse>(AuthErrors.RateLimited);
        }

        // Tenant Boundary Provisioning - In this multi-tenant architecture, Host user.Id establishes the tenant boundary
        User user = new User
        {
            Id = Guid.NewGuid(),
            DisplayUsername = displayUsername,
            NormalizedUsername = normalizedUsername,
            PasswordHash = passwordHash,
            Role = UserRole.Host,
            Status = UserStatus.Active,
            TokenSecurityVersion = 1,
            Revision = 1,
            TerminationPending = false
        };

        _dbContext.Users.Add(user);

        // Defensive Concurrency - Catches database unique constraint violations if a concurrent registration raced past the pre-check
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
