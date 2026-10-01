namespace Kahoot.Application.IntegrationTests.Features.Admin;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Admin;
using Kahoot.Application.Features.Admin.Administrators.SuspendAdministrator;
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
[Trait("Feature", "Admin")]
[Trait("Phase", "14")]
public sealed class AdministratorConcurrencyTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public AdministratorConcurrencyTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TwoAdministratorsSuspendingOneAnother_KeepAnActiveAdministrator()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        TestUserRecord adminA = await FeatureData.CreateUserAsync(harness, "SystemAdminAlpha", role: UserRole.SystemAdmin);
        TestUserRecord adminB = await FeatureData.CreateUserAsync(harness, "SystemAdminBeta", role: UserRole.SystemAdmin);

        TransactionCommitGate gate1 = new();
        await using ApplicationRequestScope scope1 = harness.CreateRequestScope(TestCaller.Admin(adminA.UserId));
        scope1.Services.GetRequiredService<ScopeConcurrencyGate>().CommitGate = gate1;
        int pid1 = await PostgresLockObserver.GetBackendPidAsync(scope1.DbContext);

        await using ApplicationRequestScope scope2 = harness.CreateRequestScope(TestCaller.Admin(adminB.UserId));
        int pid2 = await PostgresLockObserver.GetBackendPidAsync(scope2.DbContext);

        // Admin A suspends Admin B
        SuspendAdministratorCommand suspendB = new(adminB.UserId, Revision: 1);
        Task<Result> taskA = scope1.Sender.Send(suspendB);
        await gate1.Reached;

        // Admin B attempts to suspend Admin A; blocks on ordered SELECT FOR UPDATE
        SuspendAdministratorCommand suspendA = new(adminA.UserId, Revision: 1);
        Task<Result> taskB = scope2.Sender.Send(suspendA);
        await PostgresLockObserver.WaitForBlockerAsync(harness.ConnectionString, pid2, pid1, TimeSpan.FromSeconds(5));

        try
        {
            gate1.Release();

            Result resultA = await taskA;
            Assert.True(resultA.IsSuccess);

            Result resultB = await taskB;
            Assert.True(resultB.IsFailure);
            Assert.Equal(AccountErrors.LastAdministrator.Code, resultB.Error.Code);
        }
        finally
        {
            gate1.Release();
        }

        // Database invariant: exactly one active SystemAdmin remains
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int activeAdmins = await dbContext.Users
                .CountAsync(u => u.Role == UserRole.SystemAdmin && u.Status == UserStatus.Active);
            Assert.Equal(1, activeAdmins);

            User userB = await dbContext.Users.FirstAsync(u => u.Id == adminB.UserId);
            Assert.Equal(UserStatus.Suspended, userB.Status);

            User userA = await dbContext.Users.FirstAsync(u => u.Id == adminA.UserId);
            Assert.Equal(UserStatus.Active, userA.Status);
        });
    }
}
