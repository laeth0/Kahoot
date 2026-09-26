using FluentValidation;
using FluentValidation.Results;
using Kahoot.Application.Common.Exceptions;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Options;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Seeding;
using Kahoot.Application.Features.Auth.Register;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kahoot.Application.Features.Auth.Bootstrap;

internal sealed class SystemAdminSeeder : ISeeder
{
    private const string NormalizedUsernameConstraintName = "ux_users_normalized_username";

    private readonly IAppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IValidator<RegisterCommand> _credentialValidator;
    private readonly BootstrapAdminOptions _options;
    private readonly ILogger<SystemAdminSeeder> _logger;

    public SystemAdminSeeder(
        IAppDbContext dbContext,
        IPasswordHasher passwordHasher,
        IValidator<RegisterCommand> credentialValidator,
        IOptions<BootstrapAdminOptions> options,
        ILogger<SystemAdminSeeder> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _credentialValidator = credentialValidator;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        RegisterCommand credentials = new RegisterCommand(_options.Username, _options.Password);
        ValidationResult validation = await _credentialValidator.ValidateAsync(credentials, cancellationToken);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException("Bootstrap administrator credentials do not meet the account requirements.");
        }

        string displayUsername = UsernameNormalization.GetDisplayUsername(_options.Username);
        string normalizedUsername = UsernameNormalization.GetNormalizedUsername(displayUsername);
        if (IsAlreadySeeded(await GetExistingRoleAsync(normalizedUsername, cancellationToken)))
        {
            return;
        }

        string passwordHash = await _passwordHasher.HashPasswordAsync(_options.Password, cancellationToken);
        User user = new User
        {
            Id = Guid.NewGuid(),
            DisplayUsername = displayUsername,
            NormalizedUsername = normalizedUsername,
            PasswordHash = passwordHash,
            Role = UserRole.SystemAdmin,
            Status = UserStatus.Active,
            TokenSecurityVersion = 1,
            Revision = 1,
            TerminationPending = false
        };

        try
        {
            await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Bootstrap administrator created. EventName={EventName}", "BootstrapAdminCreated");
        }
        catch (UniqueConstraintViolationException exception) when (
            string.Equals(exception.ConstraintName, NormalizedUsernameConstraintName, StringComparison.Ordinal))
        {
            _dbContext.Users.Remove(user);
            if (IsAlreadySeeded(await GetExistingRoleAsync(normalizedUsername, cancellationToken)))
            {
                return;
            }

            throw;
        }
    }

    private async Task<UserRole?> GetExistingRoleAsync(string normalizedUsername, CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.NormalizedUsername == normalizedUsername)
            .Select(user => (UserRole?)user.Role)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private bool IsAlreadySeeded(UserRole? existingRole)
    {
        if (existingRole == UserRole.SystemAdmin)
        {
            _logger.LogInformation("Bootstrap administrator already exists. EventName={EventName}", "BootstrapAdminAlreadyExists");
            return true;
        }

        if (existingRole is not null)
        {
            throw new InvalidOperationException("Bootstrap administrator username belongs to a Host account.");
        }

        return false;
    }
}
