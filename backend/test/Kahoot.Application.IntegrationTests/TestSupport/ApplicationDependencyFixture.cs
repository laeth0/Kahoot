namespace Kahoot.Application.IntegrationTests.TestSupport;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

public sealed class ApplicationDependencyFixture : IAsyncLifetime
{
    // Immutable image digests corresponding to local/Compose PostgreSQL 17 and Redis 7.4.11-alpine
    // postgres:17 @ sha256:d74eeac9a635390a49bc21bd49fccd973de707e2a53a76ac49b552b8712ec46f
    // redis:7.4.11-alpine @ sha256:858f009f9709ce576febc734aa78b8f6d624b82571f9ddb6bda4377c833b3499
    public const string PostgresImage = "postgres:17";
    public const string RedisImage = "redis:7.4.11-alpine";

    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RedisContainer _redisContainer;
    private IConnectionMultiplexer? _redisMultiplexer;

    public ApplicationDependencyFixture()
    {
        _postgresContainer = new PostgreSqlBuilder(PostgresImage)
            .Build();

        _redisContainer = new RedisBuilder(RedisImage)
            .Build();
    }

    public string PostgresConnectionString => _postgresContainer.GetConnectionString();

    public string RedisConnectionString => _redisContainer.GetConnectionString();

    public IConnectionMultiplexer RedisMultiplexer => _redisMultiplexer ?? throw new InvalidOperationException("Fixture is not initialized.");

    public async Task InitializeAsync()
    {
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(180));
        CancellationToken cancellationToken = timeoutCts.Token;

        await Task.WhenAll(
            _postgresContainer.StartAsync(cancellationToken),
            _redisContainer.StartAsync(cancellationToken));

        _redisMultiplexer = await ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString());
    }

    public async Task<ApplicationTestHarness> CreateHarnessAsync(
        Action<IServiceCollection>? configureServices = null,
        Action<IServiceCollection, IConfiguration>? configureServicesWithConfig = null,
        CancellationToken cancellationToken = default)
    {
        DatabaseSandbox sandbox = new(_postgresContainer.GetConnectionString());
        try
        {
            await sandbox.InitializeAsync(cancellationToken);

            string redisPrefix = $"kahoot-it-{Guid.NewGuid():N}";
            ServiceProvider serviceProvider = ApplicationServices.BuildServiceProvider(
                sandbox.ConnectionString,
                _redisContainer.GetConnectionString(),
                redisPrefix,
                RedisMultiplexer,
                configureServices,
                configureServicesWithConfig);

            return new ApplicationTestHarness(sandbox, serviceProvider, redisPrefix, RedisMultiplexer);
        }
        catch
        {
            await sandbox.DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (_redisMultiplexer is not null)
        {
            await _redisMultiplexer.DisposeAsync();
        }

        await Task.WhenAll(
            _postgresContainer.DisposeAsync().AsTask(),
            _redisContainer.DisposeAsync().AsTask());
    }
}
