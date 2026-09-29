namespace Kahoot.Infrastructure.ServiceCollectionExtension;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Options;
using Kahoot.Infrastructure.Realtime;
using Kahoot.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

// Realtime Infrastructure Installer - Configures Redis backplane for SignalR, presence leases, pub/sub subscribers, and realtime game services.
public static class RealtimeInstaller
{
    // Realtime Registration Pipeline - Sets up Redis-backed SignalR scale-out and registers presence workers.
    public static IServiceCollection AddRealtime(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GameJoinOptions>()
            .Bind(configuration.GetRequiredSection(GameJoinOptions.SectionName))
            .Validate(GameJoinOptions.HasValidOrigin,
                "GameJoin:ClientBaseUrl must be an HTTPS origin or a loopback HTTP origin.")
            .ValidateOnStart();

        services.AddOptions<RealtimeOptions>()
            .Bind(configuration.GetRequiredSection(RealtimeOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.RedisConnectionString),
                "Realtime:RedisConnectionString is required.")
            .Validate(RealtimeOptions.HasValidChannelPrefix,
                "Realtime:ChannelPrefix must be a non-empty ASCII identifier of at most 64 characters.")
            .ValidateOnStart();

        string redisConnectionString = configuration["Realtime:RedisConnectionString"] ?? string.Empty;
        string channelPrefix = configuration["Realtime:ChannelPrefix"] ?? string.Empty;

        // Redis SignalR Scale-Out Backplane - Enables multi-pod SignalR messaging via Redis pub/sub.
        services.AddSignalR()
            .AddStackExchangeRedis(redisConnectionString, options =>
            {
                options.Configuration.ChannelPrefix = RedisChannel.Literal(channelPrefix);
            });

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
        services.AddSingleton<HostPresenceService>();
        services.AddSingleton<PlayerPresenceService>();
        services.AddSingleton<IPlayerPresenceService>(sp => sp.GetRequiredService<PlayerPresenceService>());
        services.AddHostedService<HostPresenceHeartbeatWorker>();
        services.AddHostedService<PlayerPresenceHeartbeatWorker>();
        services.AddHostedService<PlayerSocketEvictionSubscriber>();
        services.AddHostedService<SocketEvictionSubscriber>();
        services.AddScoped<IGameNotificationService, GameNotificationService>();
        services.AddScoped<IPinGeneratorService, PinGeneratorService>();
        services.AddScoped<IGameCommandIdempotencyService, GameCommandIdempotencyService>();

        return services;
    }
}
