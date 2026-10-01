namespace Kahoot.Application.IntegrationTests.Features.Admin;

using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Admin;
using Kahoot.Application.Features.Admin.Administrators;
using Kahoot.Application.Features.Admin.Administrators.ListAdministrators;
using Kahoot.Application.Features.Admin.Users.GetUserById;
using Kahoot.Application.Features.Admin.Users.ListUsers;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Enums;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Admin")]
[Trait("Phase", "12")]
public sealed class AdministrationQueryTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public AdministrationQueryTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ListUsers_FiltersAndPaginatesHostAccountsOnly()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord admin = await FeatureData.CreateUserAsync(harness, "SuperAdminQuery", role: UserRole.SystemAdmin);

        TestUserRecord host1 = await FeatureData.CreateUserAsync(harness, "AlphaHost", role: UserRole.Host, status: UserStatus.Active);
        TestUserRecord host2 = await FeatureData.CreateUserAsync(harness, "BetaHost", role: UserRole.Host, status: UserStatus.Suspended);
        TestUserRecord host3 = await FeatureData.CreateUserAsync(harness, "GammaHost", role: UserRole.Host, status: UserStatus.Active);

        // 1. Pagination: PageSize = 2
        ListUsersQuery page1Query = new(Cursor: null, PageSize: 2);
        Result<ListUsersResponse> page1Result = await harness.SendAsync(page1Query, TestCaller.Admin(admin.UserId));
        Assert.True(page1Result.IsSuccess);
        Assert.Equal(2, page1Result.Value.Items.Count);
        Assert.True(page1Result.Value.HasMore);
        Assert.NotNull(page1Result.Value.NextCursor);

        // Verify Admin is never returned in ListUsers
        Assert.DoesNotContain(page1Result.Value.Items, u => u.AccountKind == UserRole.SystemAdmin);

        // Page 2
        ListUsersQuery page2Query = new(Cursor: page1Result.Value.NextCursor, PageSize: 2);
        Result<ListUsersResponse> page2Result = await harness.SendAsync(page2Query, TestCaller.Admin(admin.UserId));
        Assert.True(page2Result.IsSuccess);
        Assert.Single(page2Result.Value.Items);
        Assert.False(page2Result.Value.HasMore);
        Assert.Null(page2Result.Value.NextCursor);

        // 2. Status filtering (Suspended only)
        ListUsersQuery suspendedQuery = new(Cursor: null, PageSize: 50, Status: UserStatus.Suspended);
        Result<ListUsersResponse> suspendedResult = await harness.SendAsync(suspendedQuery, TestCaller.Admin(admin.UserId));
        Assert.True(suspendedResult.IsSuccess);
        Assert.Single(suspendedResult.Value.Items);
        Assert.Equal(host2.UserId, suspendedResult.Value.Items[0].AccountId);
        Assert.Equal(UserStatus.Suspended, suspendedResult.Value.Items[0].Status);

        // 3. Prefix search ("Alpha")
        ListUsersQuery prefixQuery = new(Cursor: null, PageSize: 50, Username: "Alpha");
        Result<ListUsersResponse> prefixResult = await harness.SendAsync(prefixQuery, TestCaller.Admin(admin.UserId));
        Assert.True(prefixResult.IsSuccess);
        Assert.Single(prefixResult.Value.Items);
        Assert.Equal("AlphaHost", prefixResult.Value.Items[0].Username);
    }

    [Fact]
    public async Task GetUserById_ReturnsHostMetadataAndHidesSecrets()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord admin = await FeatureData.CreateUserAsync(harness, "DetailAdmin", role: UserRole.SystemAdmin);
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "DetailHost", role: UserRole.Host);

        // Valid Host lookup
        GetUserByIdQuery query = new(host.UserId);
        Result<UserAdminResponse> result = await harness.SendAsync(query, TestCaller.Admin(admin.UserId));
        Assert.True(result.IsSuccess);
        UserAdminResponse response = result.Value;
        Assert.Equal(host.UserId, response.AccountId);
        Assert.Equal("DetailHost", response.Username);
        Assert.Equal(UserRole.Host, response.AccountKind);
        Assert.Equal(UserStatus.Active, response.Status);

        // Looking up an Administrator by ID in GetUserById fails with NotFound (scoped to Hosts)
        GetUserByIdQuery adminLookup = new(admin.UserId);
        Result<UserAdminResponse> adminLookupResult = await harness.SendAsync(adminLookup, TestCaller.Admin(admin.UserId));
        Assert.True(adminLookupResult.IsFailure);
        Assert.Equal(AccountErrors.NotFound.Code, adminLookupResult.Error.Code);

        // Nonexistent user lookup
        GetUserByIdQuery nonExistent = new(Guid.NewGuid());
        Result<UserAdminResponse> nonExistentResult = await harness.SendAsync(nonExistent, TestCaller.Admin(admin.UserId));
        Assert.True(nonExistentResult.IsFailure);
        Assert.Equal(AccountErrors.NotFound.Code, nonExistentResult.Error.Code);
    }

    [Fact]
    public async Task ListAdministrators_ReturnsOnlyAdminsAndEnforcesAdminRole()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord admin1 = await FeatureData.CreateUserAsync(harness, "FirstSysAdmin", role: UserRole.SystemAdmin);
        TestUserRecord admin2 = await FeatureData.CreateUserAsync(harness, "SecondSysAdmin", role: UserRole.SystemAdmin);
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "JustAHost", role: UserRole.Host);

        ListAdministratorsQuery query = new();
        Result<IReadOnlyList<AdministratorResponse>> result = await harness.SendAsync(query, TestCaller.Admin(admin1.UserId));

        Assert.True(result.IsSuccess);
        IReadOnlyList<AdministratorResponse> admins = result.Value;
        Assert.Equal(2, admins.Count);
        Assert.All(admins, a => Assert.Equal(UserRole.SystemAdmin, a.AccountKind));
        Assert.DoesNotContain(admins, a => a.AccountId == host.UserId);

        // Host caller is rejected with Forbidden
        Result<IReadOnlyList<AdministratorResponse>> forbiddenResult = await harness.SendAsync(query, TestCaller.Host(host.UserId));
        Assert.True(forbiddenResult.IsFailure);
        Assert.Equal(AuthErrors.Forbidden.Code, forbiddenResult.Error.Code);
    }
}
