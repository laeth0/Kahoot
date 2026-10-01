namespace Kahoot.Application.IntegrationTests.Features.Admin;

using Kahoot.Application.Common.Options;
using Kahoot.Application.Common.Seeding;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Admin")]
[Trait("Phase", "12")]
public sealed class BootstrapSeederTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public BootstrapSeederTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BootstrapDisabled_DoesNotCreateAdministrator()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync(services =>
        {
            services.Configure<BootstrapAdminOptions>(opts =>
            {
                opts.Enabled = false;
            });
        });

        using (IServiceScope scope = harness.Services.CreateScope())
        {
            ISeeder seeder = scope.ServiceProvider.GetRequiredService<ISeeder>();
            await seeder.SeedAsync(CancellationToken.None);
        }

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            int adminCount = await dbContext.Users.CountAsync(u => u.Role == UserRole.SystemAdmin);
            Assert.Equal(0, adminCount);
        });
    }

    [Fact]
    public async Task BootstrapEnabled_ValidCredentials_CreatesSingleAdministratorAndIsIdempotent()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync(services =>
        {
            services.Configure<BootstrapAdminOptions>(opts =>
            {
                opts.Enabled = true;
                opts.Username = "BootstrapAdminUser";
                opts.Password = "SuperSecurePassword123!@#";
            });
        });

        // 1. Initial seeding
        using (IServiceScope scope = harness.Services.CreateScope())
        {
            ISeeder seeder = scope.ServiceProvider.GetRequiredService<ISeeder>();
            await seeder.SeedAsync(CancellationToken.None);
        }

        Guid createdAdminId = Guid.Empty;
        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            List<User> admins = await dbContext.Users
                .Where(u => u.Role == UserRole.SystemAdmin)
                .ToListAsync();

            Assert.Single(admins);
            User admin = admins[0];
            Assert.Equal("BootstrapAdminUser", admin.DisplayUsername);
            Assert.Equal("BOOTSTRAPADMINUSER", admin.NormalizedUsername);
            Assert.Equal(UserRole.SystemAdmin, admin.Role);
            Assert.Equal(UserStatus.Active, admin.Status);
            createdAdminId = admin.Id;
        });

        // 2. Re-seeding (idempotent)
        using (IServiceScope scope = harness.Services.CreateScope())
        {
            ISeeder seeder = scope.ServiceProvider.GetRequiredService<ISeeder>();
            await seeder.SeedAsync(CancellationToken.None);
        }

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            List<User> admins = await dbContext.Users
                .Where(u => u.Role == UserRole.SystemAdmin)
                .ToListAsync();

            Assert.Single(admins);
            Assert.Equal(createdAdminId, admins[0].Id);
        });
    }

    [Fact]
    public async Task BootstrapEnabled_UsernameHeldByHost_ThrowsInvalidOperationException()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync(services =>
        {
            services.Configure<BootstrapAdminOptions>(opts =>
            {
                opts.Enabled = true;
                opts.Username = "TakenByHost";
                opts.Password = "SuperSecurePassword123!@#";
            });
        });

        // Pre-seed a Host account with the exact same username
        await FeatureData.CreateUserAsync(harness, "TakenByHost", role: UserRole.Host);

        using (IServiceScope scope = harness.Services.CreateScope())
        {
            ISeeder seeder = scope.ServiceProvider.GetRequiredService<ISeeder>();
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await seeder.SeedAsync(CancellationToken.None));

            Assert.Contains("belongs to a Host account", exception.Message);
        }

        await harness.ReadDbAsync(async (AppDbContext dbContext) =>
        {
            User? user = await dbContext.Users.FirstOrDefaultAsync(u => u.NormalizedUsername == "TAKENBYHOST");
            Assert.NotNull(user);
            Assert.Equal(UserRole.Host, user.Role); // No takeover occurred!

            int adminCount = await dbContext.Users.CountAsync(u => u.Role == UserRole.SystemAdmin);
            Assert.Equal(0, adminCount);
        });
    }
}
