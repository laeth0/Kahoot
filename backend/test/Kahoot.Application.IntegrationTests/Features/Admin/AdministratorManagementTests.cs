namespace Kahoot.Application.IntegrationTests.Features.Admin;

using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Admin;
using Kahoot.Application.Features.Admin.Administrators;
using Kahoot.Application.Features.Admin.Administrators.CreateAdministrator;
using Kahoot.Application.Features.Admin.Administrators.SuspendAdministrator;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Admin")]
[Trait("Phase", "12")]
public sealed class AdministratorManagementTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public AdministratorManagementTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateAdministrator_ValidCredentials_PersistsAdminAndEnforcesUniqueness()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord admin1 = await FeatureData.CreateUserAsync(harness, "RootAdmin", role: UserRole.SystemAdmin);
        TestUserRecord existingHost = await FeatureData.CreateUserAsync(harness, "HostAccount", role: UserRole.Host);

        // 1. Valid Admin creation
        CreateAdministratorCommand createCommand = new("NewAdminAccount", "StrongAdminPassword123!@#");
        Result<AdministratorResponse> createResult = await harness.SendAsync(createCommand, TestCaller.Admin(admin1.UserId));

        Assert.True(createResult.IsSuccess);
        AdministratorResponse response = createResult.Value;
        Assert.Equal("NewAdminAccount", response.Username);
        Assert.Equal(UserRole.SystemAdmin, response.AccountKind);
        Assert.Equal(UserStatus.Active, response.Status);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            User? newAdmin = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == response.AccountId);
            Assert.NotNull(newAdmin);
            Assert.Equal("NEWADMINACCOUNT", newAdmin.NormalizedUsername);
            Assert.Equal(admin1.UserId, newAdmin.CreatedBy);
        });

        // 2. Conflict against Host username
        CreateAdministratorCommand conflictHost = new("HostAccount", "AnotherPassword123!@#");
        Result<AdministratorResponse> hostConflictResult = await harness.SendAsync(conflictHost, TestCaller.Admin(admin1.UserId));
        Assert.True(hostConflictResult.IsFailure);
        Assert.Equal(AccountErrors.Conflict.Code, hostConflictResult.Error.Code);

        // 3. Conflict against existing Admin username
        CreateAdministratorCommand conflictAdmin = new("NewAdminAccount", "AnotherPassword123!@#");
        Result<AdministratorResponse> adminConflictResult = await harness.SendAsync(conflictAdmin, TestCaller.Admin(admin1.UserId));
        Assert.True(adminConflictResult.IsFailure);
        Assert.Equal(AccountErrors.Conflict.Code, adminConflictResult.Error.Code);
    }

    [Fact]
    public async Task SuspendAdministrator_OneOfTwoAdmins_SuspendsAndRevokesTokens()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord admin1 = await FeatureData.CreateUserAsync(harness, "FirstAdmin", role: UserRole.SystemAdmin);
        TestUserRecord admin2 = await FeatureData.CreateUserAsync(harness, "SecondAdmin", role: UserRole.SystemAdmin);

        // Seed refresh token for Admin 2
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Admin(admin1.UserId)))
        {
            scope.DbContext.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = admin2.UserId,
                TokenFamilyId = Guid.NewGuid(),
                TokenHash = new byte[32],
                CreatedAt = now,
                FamilyCreatedAt = now,
                ExpiresAt = now.AddDays(14),
                RevokedAt = null
            });
            await scope.DbContext.SaveChangesAsync();
        }

        SuspendAdministratorCommand suspendCommand = new(admin2.UserId, Revision: 1);
        Result suspendResult = await harness.SendAsync(suspendCommand, TestCaller.Admin(admin1.UserId));
        Assert.True(suspendResult.IsSuccess);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            User suspendedAdmin = await dbContext.Users.FirstAsync(u => u.Id == admin2.UserId);
            Assert.Equal(UserStatus.Suspended, suspendedAdmin.Status);
            Assert.Equal(2, suspendedAdmin.Revision);

            List<RefreshToken> tokens = await dbContext.RefreshTokens
                .Where(t => t.UserId == admin2.UserId)
                .ToListAsync();
            Assert.All(tokens, t => Assert.NotNull(t.RevokedAt));

            User activeAdmin = await dbContext.Users.FirstAsync(u => u.Id == admin1.UserId);
            Assert.Equal(UserStatus.Active, activeAdmin.Status);
        });
    }

    [Fact]
    public async Task SuspendAdministrator_SoleActiveAdmin_ReturnsLastAdministrator()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord soleAdmin = await FeatureData.CreateUserAsync(harness, "SoleAdmin", role: UserRole.SystemAdmin);

        SuspendAdministratorCommand suspendCommand = new(soleAdmin.UserId, Revision: 1);
        Result suspendResult = await harness.SendAsync(suspendCommand, TestCaller.Admin(soleAdmin.UserId));

        Assert.True(suspendResult.IsFailure);
        Assert.Equal(AccountErrors.LastAdministrator.Code, suspendResult.Error.Code);

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            User admin = await dbContext.Users.FirstAsync(u => u.Id == soleAdmin.UserId);
            Assert.Equal(UserStatus.Active, admin.Status);
            Assert.Equal(1, admin.Revision);
        });
    }

    [Fact]
    public async Task AdministratorManagement_NonAdminCaller_ReturnsForbidden()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "HostCaller", role: UserRole.Host);
        TestUserRecord admin = await FeatureData.CreateUserAsync(harness, "TargetAdmin", role: UserRole.SystemAdmin);

        CreateAdministratorCommand createCmd = new("HackerAdmin", "Pass123456!@#");
        Result<AdministratorResponse> createResult = await harness.SendAsync(createCmd, TestCaller.Host(host.UserId));
        Assert.True(createResult.IsFailure);
        Assert.Equal(AuthErrors.Forbidden.Code, createResult.Error.Code);

        SuspendAdministratorCommand suspendCmd = new(admin.UserId, Revision: 1);
        Result suspendResult = await harness.SendAsync(suspendCmd, TestCaller.Host(host.UserId));
        Assert.True(suspendResult.IsFailure);
        Assert.Equal(AuthErrors.Forbidden.Code, suspendResult.Error.Code);
    }
}
