namespace Kahoot.Application.IntegrationTests.Features.Auth;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Auth.Register;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Auth")]
[Trait("Phase", "03")]
public sealed class RegistrationTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public RegistrationTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Register_NewAccount_PersistsActiveHostWithAuditedDefaults()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        RegisterCommand command = new("NewHostAccount", "ValidPassword123!@#");
        Result<RegisterResponse> result = await harness.SendAsync(command, TestCaller.Anonymous);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.AccountId);
        Assert.Equal("NewHostAccount", result.Value.Username);

        User? persistedUser = await harness.ReadDbAsync(async (db, ct) =>
            await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == result.Value.AccountId, ct));

        Assert.NotNull(persistedUser);
        Assert.Equal("NewHostAccount", persistedUser.DisplayUsername);
        Assert.Equal("NEWHOSTACCOUNT", persistedUser.NormalizedUsername);
        Assert.Equal(UserRole.Host, persistedUser.Role);
        Assert.Equal(UserStatus.Active, persistedUser.Status);
        Assert.Equal(1L, persistedUser.Revision);
        Assert.Equal(1, persistedUser.TokenSecurityVersion);
        Assert.False(persistedUser.TerminationPending);

        IPasswordHasher passwordHasher = harness.Services.GetRequiredService<IPasswordHasher>();
        bool passwordMatches = await passwordHasher.VerifyPasswordAsync("ValidPassword123!@#", persistedUser.PasswordHash);
        Assert.True(passwordMatches);
    }

    [Fact]
    public async Task Register_NormalizedDuplicate_ReturnsUsernameUnavailable()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        RegisterCommand firstCommand = new("DuplicateUser", "ValidPassword123!@#");
        Result<RegisterResponse> firstResult = await harness.SendAsync(firstCommand, TestCaller.Anonymous);
        Assert.True(firstResult.IsSuccess);

        RegisterCommand duplicateCommand = new("duplicateuser", "AnotherValidPassword123!@#");
        Result<RegisterResponse> duplicateResult = await harness.SendAsync(duplicateCommand, TestCaller.Anonymous);

        Assert.False(duplicateResult.IsSuccess);
        Assert.Equal(AuthErrors.UsernameUnavailable.Code, duplicateResult.Error.Code);

        int matchingUsers = await harness.ReadDbAsync(async (db, ct) =>
            await db.Users.CountAsync(u => u.NormalizedUsername == "DUPLICATEUSER", ct));
        int tokenCount = await harness.ReadDbAsync(async (db, ct) =>
            await db.RefreshTokens.CountAsync(ct));

        Assert.Equal(1, matchingUsers);
        Assert.Equal(0, tokenCount);
    }

    [Fact]
    public async Task Register_UsernameHeldByAdministrator_IsUnavailable()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord admin = await FeatureData.CreateUserAsync(
            harness,
            displayUsername: "SystemAdminUser",
            role: UserRole.SystemAdmin);

        RegisterCommand command = new("systemadminuser", "ValidPassword123!@#");
        Result<RegisterResponse> result = await harness.SendAsync(command, TestCaller.Anonymous);

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrors.UsernameUnavailable.Code, result.Error.Code);

        int totalUsers = await harness.ReadDbAsync(async (db, ct) =>
            await db.Users.CountAsync(ct));

        Assert.Equal(1, totalUsers);
    }
}
