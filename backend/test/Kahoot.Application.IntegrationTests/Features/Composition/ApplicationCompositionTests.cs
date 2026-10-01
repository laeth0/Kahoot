namespace Kahoot.Application.IntegrationTests.Features.Composition;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.IntegrationTests.TestSupport;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StackExchange.Redis;
using Xunit;

[Collection("ApplicationIntegrationCollection")]
[Trait("Category", "Integration")]
[Trait("Feature", "Composition")]
[Trait("Phase", "01")]
public sealed class ApplicationCompositionTests
{
    private readonly ApplicationDependencyFixture _fixture;

    public ApplicationCompositionTests(ApplicationDependencyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Composition_UsesProductionContextAndScopedRequestIdentity()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        Guid hostAId = Guid.NewGuid();
        Guid hostBId = Guid.NewGuid();

        TestCaller callerA = TestCaller.Host(hostAId);
        TestCaller callerB = TestCaller.Host(hostBId);
        TestCaller callerAnon = TestCaller.Anonymous;

        await using (ApplicationRequestScope scopeA = harness.CreateRequestScope(callerA))
        {
            IAppDbContext appDbContextA = scopeA.Services.GetRequiredService<IAppDbContext>();
            AppDbContext concreteDbContextA = scopeA.Services.GetRequiredService<AppDbContext>();
            ICurrentUser currentUserA = scopeA.Services.GetRequiredService<ICurrentUser>();

            Assert.Same(concreteDbContextA, appDbContextA);
            Assert.Same(scopeA.DbContext, concreteDbContextA);
            Assert.True(currentUserA.IsAuthenticated);
            Assert.Equal(hostAId, currentUserA.UserId);
            Assert.Equal("Host", currentUserA.Role);

            await using (ApplicationRequestScope scopeB = harness.CreateRequestScope(callerB))
            {
                AppDbContext concreteDbContextB = scopeB.Services.GetRequiredService<AppDbContext>();
                ICurrentUser currentUserB = scopeB.Services.GetRequiredService<ICurrentUser>();

                Assert.NotSame(concreteDbContextA, concreteDbContextB);
                Assert.True(currentUserB.IsAuthenticated);
                Assert.Equal(hostBId, currentUserB.UserId);
                Assert.Equal("Host", currentUserB.Role);

                // Scope A identity must not be mutated by Scope B creation
                Assert.Equal(hostAId, currentUserA.UserId);
            }

            await using (ApplicationRequestScope scopeAnon = harness.CreateRequestScope(callerAnon))
            {
                ICurrentUser currentUserAnon = scopeAnon.Services.GetRequiredService<ICurrentUser>();

                Assert.False(currentUserAnon.IsAuthenticated);
                Assert.Null(currentUserAnon.UserId);
                Assert.Null(currentUserAnon.Role);

                // Scope A identity still intact
                Assert.Equal(hostAId, currentUserA.UserId);
            }
        }
    }

    [Fact]
    public async Task MigratedDatabase_RoundTripsNativeEnums()
    {
        await using ApplicationTestHarness harness = await _fixture.CreateHarnessAsync();

        Guid hostId = Guid.NewGuid();
        Guid quizId = Guid.NewGuid();
        Guid gameId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await using (ApplicationRequestScope scope = harness.CreateRequestScope(TestCaller.Host(hostId)))
        {
            User user = new()
            {
                Id = hostId,
                DisplayUsername = "HostUser",
                NormalizedUsername = "HOSTUSER",
                PasswordHash = "dummy_hash_for_enum_test",
                Role = UserRole.Host,
                Status = UserStatus.Active,
                TokenSecurityVersion = 1,
                Revision = 1,
                TerminationPending = false,
                CreatedAt = now,
                UpdatedAt = now
            };

            Quiz quiz = new()
            {
                Id = quizId,
                HostAccountId = hostId,
                Title = "Enum Test Quiz",
                Description = "A test quiz for enum roundtrip",
                Revision = 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            Game game = new()
            {
                Id = gameId,
                HostAccountId = hostId,
                SourceQuizId = quizId,
                Title = "Enum Test Game",
                Pin = "123456",
                Status = GameStatus.Lobby,
                StateVersion = 1,
                PresenceVersion = 0,
                ReservedParticipantCount = 0,
                NextSeatNumber = 1,
                CreatedAt = now
            };

            scope.DbContext.Users.Add(user);
            scope.DbContext.Quizzes.Add(quiz);
            scope.DbContext.Games.Add(game);
            await scope.DbContext.SaveChangesAsync();
        }

        User? readUser = await harness.ReadDbAsync(async (db, ct) =>
            await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == hostId, ct));

        Game? readGame = await harness.ReadDbAsync(async (db, ct) =>
            await db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gameId, ct));

        Assert.NotNull(readUser);
        Assert.Equal(UserRole.Host, readUser.Role);
        Assert.Equal(UserStatus.Active, readUser.Status);

        Assert.NotNull(readGame);
        Assert.Equal(GameStatus.Lobby, readGame.Status);
    }

    [Fact]
    public async Task SeparateHarnesses_DoNotShareDatabaseRowsOrRedisKeys()
    {
        await using ApplicationTestHarness harnessA = await _fixture.CreateHarnessAsync();
        await using ApplicationTestHarness harnessB = await _fixture.CreateHarnessAsync();

        Guid userIdA = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await using (ApplicationRequestScope scopeA = harnessA.CreateRequestScope(TestCaller.Host(userIdA)))
        {
            User userA = new()
            {
                Id = userIdA,
                DisplayUsername = "IsolationUserA",
                NormalizedUsername = "ISOLATIONUSERA",
                PasswordHash = "dummy_hash_for_isolation_test",
                Role = UserRole.Host,
                Status = UserStatus.Active,
                TokenSecurityVersion = 1,
                Revision = 1,
                TerminationPending = false,
                CreatedAt = now,
                UpdatedAt = now
            };

            scopeA.DbContext.Users.Add(userA);
            await scopeA.DbContext.SaveChangesAsync();
        }

        IDatabase redisDb = _fixture.RedisMultiplexer.GetDatabase();
        string keyA = $"{harnessA.RedisChannelPrefix}:marker";
        await redisDb.StringSetAsync(keyA, "value_from_a");

        User? userInB = await harnessB.ReadDbAsync(async (db, ct) =>
            await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userIdA, ct));

        string keyB = $"{harnessB.RedisChannelPrefix}:marker";
        RedisValue redisValInB = await redisDb.StringGetAsync(keyB);

        Assert.Null(userInB);
        Assert.True(redisValInB.IsNullOrEmpty);
    }

    [Fact]
    public async Task Harness_DisposalRemovesOwnedResources()
    {
        ApplicationTestHarness harness1 = await _fixture.CreateHarnessAsync();
        await using ApplicationTestHarness harness2 = await _fixture.CreateHarnessAsync();

        string dbName1 = harness1.Sandbox.DatabaseName;
        IDatabase redisDb = _fixture.RedisMultiplexer.GetDatabase();
        string key1 = $"{harness1.RedisChannelPrefix}:marker";
        await redisDb.StringSetAsync(key1, "disposal_marker");

        // Verify key exists before disposal
        RedisValue valueBefore = await redisDb.StringGetAsync(key1);
        Assert.False(valueBefore.IsNullOrEmpty);

        // Explicitly dispose harness1
        await harness1.DisposeAsync();

        // 1. Verify Redis key is cleaned up
        RedisValue valueAfter = await redisDb.StringGetAsync(key1);
        Assert.True(valueAfter.IsNullOrEmpty);

        // 2. Verify database was dropped
        await using (NpgsqlConnection adminConn = new(_fixture.PostgresConnectionString))
        {
            await adminConn.OpenAsync();
            await using NpgsqlCommand checkCmd = adminConn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM pg_database WHERE datname = @name;";
            checkCmd.Parameters.AddWithValue("@name", dbName1);
            object? countObj = await checkCmd.ExecuteScalarAsync();
            long dbCount = Convert.ToInt64(countObj);
            Assert.Equal(0L, dbCount);
        }

        // 3. Verify harness2 is still operational
        Guid testId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await using (ApplicationRequestScope scope2 = harness2.CreateRequestScope(TestCaller.Host(testId)))
        {
            User user2 = new()
            {
                Id = testId,
                DisplayUsername = "StillWorksUser",
                NormalizedUsername = "STILLWORKSUSER",
                PasswordHash = "dummy_hash",
                Role = UserRole.Host,
                Status = UserStatus.Active,
                TokenSecurityVersion = 1,
                Revision = 1,
                TerminationPending = false,
                CreatedAt = now,
                UpdatedAt = now
            };

            scope2.DbContext.Users.Add(user2);
            await scope2.DbContext.SaveChangesAsync();
        }

        User? readUser2 = await harness2.ReadDbAsync(async (db, ct) =>
            await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == testId, ct));

        Assert.NotNull(readUser2);
    }
}
