using MatchMaking.Worker.Application.Abstractions;
using MatchMaking.Worker.Infrastructure.Configuration;
using MatchMaking.Worker.Infrastructure.Kafka;
using MatchMaking.Worker.Infrastructure.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace MatchMaking.Worker.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<MatchmakingOptions>(configuration.GetSection(MatchmakingOptions.SectionName));
        services.AddSingleton<IMatchCompleteProducer, MatchCompleteProducer>();

        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>()
            ?? throw new InvalidOperationException($"Configuration section '{RedisOptions.SectionName}' is missing");
        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisOptions.ConnectionString));
        services.AddSingleton<IPlayerQueue, RedisPlayerQueue>();

        services.AddHostedService<MatchRequestConsumer>();

        return services;
    }
}
