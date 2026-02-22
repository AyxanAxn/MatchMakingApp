using MatchMaking.Service.Application.Abstractions;
using MatchMaking.Service.Infrastructure.Configuration;
using MatchMaking.Service.Infrastructure.Kafka;
using MatchMaking.Service.Infrastructure.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace MatchMaking.Service.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.AddSingleton<IMatchmakingProducer, MatchmakingProducer>();

        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>()
            ?? throw new InvalidOperationException($"Configuration section '{RedisOptions.SectionName}' is missing");
        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisOptions.ConnectionString));
        services.AddScoped<IMatchRepository, RedisMatchRepository>();

        services.AddHostedService<MatchCompleteConsumer>();

        return services;
    }
}
