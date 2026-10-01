namespace Kahoot.Application.IntegrationTests.Features.Auth;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Admin.Users.SuspendUser;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Auth.Login;
using Kahoot.Application.Features.Auth.Logout;
using Kahoot.Application.Features.Auth.LogoutAll;
using Kahoot.Application.Features.Auth.Refresh;
using Kahoot.Application.Features.Auth.Register;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Application.IntegrationTests.TestSupport.Concurrency;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Auth")]
[Trait("Phase", "14")]
public sealed class AuthConcurrencyTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public AuthConcurrencyTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConcurrentRegistration_EquivalentUsernames_CommitOneAccount()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        SaveChangesGate gate1 = new();
        SaveChangesGate gate2 = new();

        await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Anonymous);
        scope1.Services.GetRequiredService<ScopeConcurrencyGate>().SaveGate = gate1;

        await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Anonymous);
        scope2.Services.GetRequiredService<ScopeConcurrencyGate>().SaveGate = gate2;

        RegisterCommand cmd1 = new("SuperHost2026", "Password123!@#");
        RegisterCommand cmd2 = new("superhost2026", "Password123!@#");

        Task<Result<RegisterResponse>> task1 = scope1.Sender.Send(cmd1);
        await gate1.Reached;

        Task<Result<RegisterResponse>> task2 = scope2.Sender.Send(cmd2);
        await gate2.Reached;

        try
        {
            // Release first registration; commits successfully
            gate1.Release();
            Result<RegisterResponse> result1 = await task1;
            Assert.True(result1.IsSuccess);

            // Release second registration; unique index on normalized_username rejects insert
            gate2.Release();
            Result<RegisterResponse> result2 = await task2;
            Assert.True(result2.IsFailure);
            Assert.Equal(AuthErrors.UsernameUnavailable.Code, result2.Error.Code);
        }
        finally
        {
            gate1.Release();
            gate2.Release();
        }

        // Exactly one user account committed
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int count = await dbContext.Users.CountAsync(u => u.NormalizedUsername == "SUPERHOST2026");
            Assert.Equal(1, count);
        });
    }

    [Fact]
    public async Task ConcurrentRefresh_SameToken_ReturnsOneReplacementAndOneRace()
    {
        TestTimeProvider clock = new(DateTimeOffset.UtcNow);
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync(services =>
        {
            services.AddSingleton<TimeProvider>(clock);
        });

        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "RefreshRaceUser", role: UserRole.Host);
        LoginResult login = await FeatureData.LoginAsync(harness, "RefreshRaceUser", "Password123!@#");
        string originalRefreshToken = login.RawRefreshToken;

        TransactionCommitGate gate1 = new();

        await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Anonymous);
        scope1.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate1;
        int pid1 = await PostgresLockObserver.GetBackendPidAsync(scope1.DbContext);

        await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Anonymous);
        int pid2 = await PostgresLockObserver.GetBackendPidAsync(scope2.DbContext);

        RefreshCommand refreshCmd = new(originalRefreshToken);

        Task<Result<RefreshResult>> task1 = scope1.Sender.Send(refreshCmd);
        await gate1.Reached;

        Task<Result<RefreshResult>> task2 = scope2.Sender.Send(refreshCmd);
        await PostgresLockObserver.WaitForBlockerAsync(harness.ConnectionString, pid2, pid1, TimeSpan.FromSeconds(5));

        try
        {
            // Operation 1 commits rotation
            gate1.Release();
            Result<RefreshResult> result1 = await task1;
            Assert.True(result1.IsSuccess);

            // Operation 2 unblocks; within 10s grace, returns RefreshRace
            Result<RefreshResult> result2 = await task2;
            Assert.True(result2.IsFailure);
            Assert.Equal(AuthErrors.RefreshRace.Code, result2.Error.Code);
        }
        finally
        {
            gate1.Release();
        }

        // Family remains valid with exactly one active replacement token
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            List<RefreshToken> tokens = await dbContext.RefreshTokens
                .Where(t => t.UserId == host.UserId)
                .ToListAsync();

            Assert.Equal(2, tokens.Count);
            Assert.Single(tokens, t => t.RotatedAt == null && t.RevokedAt == null);
        });
    }

    [Fact]
    public async Task RefreshVsLogout_RespectsBothCommitOrders()
    {
        // Order A: Refresh commits first, Logout commits second
        {
            await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
            TestUserRecord host = await FeatureData.CreateUserAsync(harness, "LogoutRaceA", role: UserRole.Host);
            LoginResult login = await FeatureData.LoginAsync(harness, "LogoutRaceA", "Password123!@#");

            TransactionCommitGate gate1 = new();
            await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Anonymous);
            scope1.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate1;
            int pid1 = await PostgresLockObserver.GetBackendPidAsync(scope1.DbContext);

            await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Anonymous);
            int pid2 = await PostgresLockObserver.GetBackendPidAsync(scope2.DbContext);

            Task<Result<RefreshResult>> refreshTask = scope1.Sender.Send(new RefreshCommand(login.RawRefreshToken));
            await gate1.Reached;

            Task<Result> logoutTask = scope2.Sender.Send(new LogoutCommand(login.RawRefreshToken));
            await PostgresLockObserver.WaitForBlockerAsync(harness.ConnectionString, pid2, pid1, TimeSpan.FromSeconds(5));

            try
            {
                gate1.Release();
                Result<RefreshResult> refreshRes = await refreshTask;
                Assert.True(refreshRes.IsSuccess);

                Result logoutRes = await logoutTask;
                Assert.True(logoutRes.IsSuccess);
            }
            finally
            {
                gate1.Release();
            }

            // Both tokens in family are revoked
            await harness.ReadDbAsync(async (AppDbContext dbContext) =>
            {
                List<RefreshToken> tokens = await dbContext.RefreshTokens
                    .Where(t => t.UserId == host.UserId)
                    .ToListAsync();
                Assert.All(tokens, t => Assert.NotNull(t.RevokedAt));
            });
        }

        // Order B: Logout commits first, Refresh commits second
        {
            await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
            TestUserRecord host = await FeatureData.CreateUserAsync(harness, "LogoutRaceB", role: UserRole.Host);
            LoginResult login = await FeatureData.LoginAsync(harness, "LogoutRaceB", "Password123!@#");

            TransactionCommitGate gate1 = new();
            await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Anonymous);
            scope1.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate1;
            int pid1 = await PostgresLockObserver.GetBackendPidAsync(scope1.DbContext);

            await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Anonymous);
            int pid2 = await PostgresLockObserver.GetBackendPidAsync(scope2.DbContext);

            Task<Result> logoutTask = scope1.Sender.Send(new LogoutCommand(login.RawRefreshToken));
            await gate1.Reached;

            Task<Result<RefreshResult>> refreshTask = scope2.Sender.Send(new RefreshCommand(login.RawRefreshToken));
            await PostgresLockObserver.WaitForBlockerAsync(harness.ConnectionString, pid2, pid1, TimeSpan.FromSeconds(5));

            try
            {
                gate1.Release();
                Result logoutRes = await logoutTask;
                Assert.True(logoutRes.IsSuccess);

                Result<RefreshResult> refreshRes = await refreshTask;
                Assert.True(refreshRes.IsFailure);
                Assert.Equal(AuthErrors.InvalidRefreshToken.Code, refreshRes.Error.Code);
            }
            finally
            {
                gate1.Release();
            }
        }
    }

    [Fact]
    public async Task RefreshVsLogoutAllOrPasswordChange_LeavesNoActiveOldFamily()
    {
        // Order A: Refresh commits first, LogoutAll commits second
        {
            await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
            TestUserRecord host = await FeatureData.CreateUserAsync(harness, "LogoutAllRaceA", role: UserRole.Host);
            LoginResult login = await FeatureData.LoginAsync(harness, "LogoutAllRaceA", "Password123!@#");

            TransactionCommitGate gate1 = new();
            await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Anonymous);
            scope1.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate1;
            int pid1 = await PostgresLockObserver.GetBackendPidAsync(scope1.DbContext);

            await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Host(host.UserId));
            int pid2 = await PostgresLockObserver.GetBackendPidAsync(scope2.DbContext);

            Task<Result<RefreshResult>> refreshTask = scope1.Sender.Send(new RefreshCommand(login.RawRefreshToken));
            await gate1.Reached;

            Task<Result> logoutAllTask = scope2.Sender.Send(new LogoutAllCommand());
            await PostgresLockObserver.WaitForBlockerAsync(harness.ConnectionString, pid2, pid1, TimeSpan.FromSeconds(5));

            try
            {
                gate1.Release();
                Result<RefreshResult> refreshRes = await refreshTask;
                Assert.True(refreshRes.IsSuccess);

                Result logoutAllRes = await logoutAllTask;
                Assert.True(logoutAllRes.IsSuccess);
            }
            finally
            {
                gate1.Release();
            }

            // All tokens revoked and security version bumped
            await harness.ReadDbAsync(async (AppDbContext dbContext) =>
            {
                User user = await dbContext.Users.FirstAsync(u => u.Id == host.UserId);
                Assert.Equal(2, user.TokenSecurityVersion);

                List<RefreshToken> tokens = await dbContext.RefreshTokens
                    .Where(t => t.UserId == host.UserId)
                    .ToListAsync();
                Assert.All(tokens, t => Assert.NotNull(t.RevokedAt));
            });
        }

        // Order B: LogoutAll commits first, Refresh commits second
        {
            await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
            TestUserRecord host = await FeatureData.CreateUserAsync(harness, "LogoutAllRaceB", role: UserRole.Host);
            LoginResult login = await FeatureData.LoginAsync(harness, "LogoutAllRaceB", "Password123!@#");

            TransactionCommitGate gate1 = new();
            await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Host(host.UserId));
            scope1.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate1;
            int pid1 = await PostgresLockObserver.GetBackendPidAsync(scope1.DbContext);

            await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Anonymous);
            int pid2 = await PostgresLockObserver.GetBackendPidAsync(scope2.DbContext);

            Task<Result> logoutAllTask = scope1.Sender.Send(new LogoutAllCommand());
            await gate1.Reached;

            Task<Result<RefreshResult>> refreshTask = scope2.Sender.Send(new RefreshCommand(login.RawRefreshToken));
            await PostgresLockObserver.WaitForBlockerAsync(harness.ConnectionString, pid2, pid1, TimeSpan.FromSeconds(5));

            try
            {
                gate1.Release();
                Result logoutAllRes = await logoutAllTask;
                Assert.True(logoutAllRes.IsSuccess);

                Result<RefreshResult> refreshRes = await refreshTask;
                Assert.True(refreshRes.IsFailure);
                Assert.Equal(AuthErrors.InvalidRefreshToken.Code, refreshRes.Error.Code);
            }
            finally
            {
                gate1.Release();
            }
        }
    }

    [Fact]
    public async Task LoginOrRefreshVsHostSuspension_ValidatesPersistedAccount()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();
        TestUserRecord admin = await FeatureData.CreateUserAsync(harness, "AdminActor", role: UserRole.SystemAdmin);
        TestUserRecord host = await FeatureData.CreateUserAsync(harness, "SuspendedActor", role: UserRole.Host);
        LoginResult login = await FeatureData.LoginAsync(harness, "SuspendedActor", "Password123!@#");

        // Suspension commits first
        TransactionCommitGate gate1 = new();
        await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Admin(admin.UserId));
        scope1.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate1;
        int pid1 = await PostgresLockObserver.GetBackendPidAsync(scope1.DbContext);

        await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Anonymous);
        int pid2 = await PostgresLockObserver.GetBackendPidAsync(scope2.DbContext);

        Task<Result<SuspendUserResult>> suspendTask = scope1.Sender.Send(new SuspendUserCommand(host.UserId, Revision: 1));
        await gate1.Reached;

        Task<Result<RefreshResult>> refreshTask = scope2.Sender.Send(new RefreshCommand(login.RawRefreshToken));
        await PostgresLockObserver.WaitForBlockerAsync(harness.ConnectionString, pid2, pid1, TimeSpan.FromSeconds(5));

        try
        {
            gate1.Release();
            Result<SuspendUserResult> suspendRes = await suspendTask;
            Assert.True(suspendRes.IsSuccess);

            // User is now suspended; Refresh returns InvalidRefreshToken
            Result<RefreshResult> refreshRes = await refreshTask;
            Assert.True(refreshRes.IsFailure);
            Assert.Equal(AuthErrors.InvalidRefreshToken.Code, refreshRes.Error.Code);
        }
        finally
        {
            gate1.Release();
        }

        // Login attempt against now-suspended host returns InvalidCredentials
        LoginCommand loginCmd = new("SuspendedActor", "Password123!@#", "127.0.0.1");
        Result<LoginResult> loginRes = await harness.SendAsync(loginCmd, TestCaller.Anonymous);
        Assert.True(loginRes.IsFailure);
        Assert.Equal(AuthErrors.InvalidCredentials.Code, loginRes.Error.Code);
    }
}
