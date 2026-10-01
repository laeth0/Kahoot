namespace Kahoot.Application.IntegrationTests.TestSupport;

using System.Security.Cryptography;
using Kahoot.Application;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Options;
using Kahoot.Infrastructure.Persistence;
using Kahoot.Infrastructure.Realtime;
using Kahoot.Infrastructure.ServiceCollectionExtension;
using Kahoot.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

internal sealed class TestCallerHolder
{
    public TestCaller Caller { get; set; } = TestCaller.Anonymous;
}

public static class ApplicationServices
{
    private static readonly byte[] StaticSigningKey = new byte[32]
    {
        0x4b, 0x61, 0x68, 0x6f, 0x6f, 0x74, 0x54, 0x65,
        0x73, 0x74, 0x53, 0x69, 0x67, 0x6e, 0x69, 0x6e,
        0x67, 0x4b, 0x65, 0x79, 0x32, 0x30, 0x32, 0x36,
        0x53, 0x65, 0x63, 0x75, 0x72, 0x65, 0x21, 0x21
    };

    public static ServiceProvider BuildServiceProvider(
        string databaseConnectionString,
        string redisConnectionString,
        string redisChannelPrefix,
        IConnectionMultiplexer connectionMultiplexer,
        Action<IServiceCollection>? configureServices = null,
        Action<IServiceCollection, IConfiguration>? configureServicesWithConfig = null)
    {
        Dictionary<string, string?> inMemorySettings = new()
        {
            ["ConnectionStrings:DefaultConnection"] = databaseConnectionString,
            ["Database:CommandTimeoutSeconds"] = "30",
            ["Database:MigrationCommandTimeoutSeconds"] = "120",
            ["Database:EnableSensitiveDataLogging"] = "false",
            ["Database:EnableDetailedErrors"] = "false",
            ["Jwt:Issuer"] = "Kahoot.IntegrationTests",
            ["Jwt:Audience"] = "Kahoot.Client",
            ["Jwt:SigningKey"] = Convert.ToBase64String(StaticSigningKey),
            ["Jwt:AccessTokenMinutes"] = "15",
            ["RefreshToken:LifetimeDays"] = "14",
            ["RefreshToken:FamilyMaxLifetimeDays"] = "90",
            ["BootstrapAdmin:Enabled"] = "false",
            ["GameJoin:ClientBaseUrl"] = "https://client.kahoot.test",
            ["Realtime:RedisConnectionString"] = redisConnectionString,
            ["Realtime:ChannelPrefix"] = redisChannelPrefix,
            ["ImageStorage:UploadsSubdirectory"] = "uploads",
            ["ImageStorage:StagingSubdirectory"] = "uploads/staging",
            ["ImageStorage:MaxFileSizeBytes"] = "5242880",
            ["ImageStorage:MaxWidth"] = "4096",
            ["ImageStorage:MaxHeight"] = "4096",
            ["ImageStorage:MaxPixelArea"] = "16777216",
            ["ImageStorage:MaxDecodeMemoryBytes"] = "67108864",
            ["ImageStorage:MinimumFreeStorageRatio"] = "0.10",
            ["ImageStorage:OrphanRetentionDays"] = "7",
            ["ImageStorage:StagingQuarantineHours"] = "24",
            ["ImageStorage:CleanupIntervalMinutes"] = "10",
            ["ImageStorage:CleanupBatchSize"] = "100",
            ["ImageStorage:MaxBatchesPerPass"] = "20",
            ["ImageStorage:ReconciliationBatchSize"] = "200"
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        ServiceCollection services = new();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddOptions();
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<TestCallerHolder>();
        services.AddScoped<TestCaller>(sp => sp.GetRequiredService<TestCallerHolder>().Caller);
        services.AddScoped<TestCurrentUser>(sp => new TestCurrentUser(sp.GetRequiredService<TestCallerHolder>().Caller));
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());

        services.AddApplication();
        services.AddPersistence(configuration);
        services.AddSecurity(configuration);

        services.AddScoped<Kahoot.Application.IntegrationTests.TestSupport.Concurrency.ScopeConcurrencyGate>();
        services.AddScoped<Kahoot.Application.IntegrationTests.TestSupport.Concurrency.TransactionCommitInterceptor>();
        services.AddScoped<Kahoot.Application.IntegrationTests.TestSupport.Concurrency.SaveChangesGateInterceptor>();

        services.AddScoped<AppDbContext>(serviceProvider =>
        {
            DbContextOptions<AppDbContext> baseOptions = serviceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();
            DbContextOptionsBuilder<AppDbContext> builder = new(baseOptions);
            builder.AddInterceptors(
                serviceProvider.GetRequiredService<Kahoot.Application.IntegrationTests.TestSupport.Concurrency.TransactionCommitInterceptor>(),
                serviceProvider.GetRequiredService<Kahoot.Application.IntegrationTests.TestSupport.Concurrency.SaveChangesGateInterceptor>());

            return new AppDbContext(builder.Options);
        });

        services.AddOptions<GameJoinOptions>()
            .Bind(configuration.GetRequiredSection(GameJoinOptions.SectionName));

        services.AddOptions<RealtimeOptions>()
            .Bind(configuration.GetRequiredSection(RealtimeOptions.SectionName));

        services.AddSingleton<IConnectionMultiplexer>(connectionMultiplexer);
        services.AddScoped<IPinGeneratorService, PinGeneratorService>();
        services.AddScoped<IGameCommandIdempotencyService, GameCommandIdempotencyService>();

        services.AddSingleton<RecordingGameNotificationService>();
        services.AddSingleton<IGameNotificationService>(sp => sp.GetRequiredService<RecordingGameNotificationService>());

        services.AddSingleton<RecordingSocketEvictionService>();
        services.AddSingleton<ISocketEvictionService>(sp => sp.GetRequiredService<RecordingSocketEvictionService>());

        services.AddSingleton<RecordingSuspensionFinalizerChannel>();
        services.AddSingleton<ISuspensionFinalizerChannel>(sp => sp.GetRequiredService<RecordingSuspensionFinalizerChannel>());

        services.AddSingleton<ControlledPlayerPresenceService>();
        services.AddSingleton<IPlayerPresenceService>(sp => sp.GetRequiredService<ControlledPlayerPresenceService>());

        configureServices?.Invoke(services);
        configureServicesWithConfig?.Invoke(services, configuration);

        if (!services.Any(d => d.ServiceType == typeof(IImageStorageService)))
        {
            services.AddSingleton<FailOnUseImageStorageService>();
            services.AddSingleton<IImageStorageService>(sp => sp.GetRequiredService<FailOnUseImageStorageService>());
        }

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }
}
