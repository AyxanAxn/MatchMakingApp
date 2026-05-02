using MatchMaking.Contracts.Configuration;
using MatchMaking.Contracts.Constants;
using MatchMaking.Contracts.Kafka;
using MatchMaking.Contracts.Messages;
using MatchMaking.Service.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MatchMaking.Service.Infrastructure.Kafka;

public sealed class MatchCompleteConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> kafkaOptions,
    ILogger<MatchCompleteConsumer> logger)
    : KafkaConsumerBase<MatchmakingComplete>(scopeFactory, kafkaOptions, logger)
{
    protected override string Topic => KafkaTopics.MatchmakingComplete;

    protected override async Task ProcessMessageAsync(
        MatchmakingComplete message,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        var repository = scope.ServiceProvider.GetRequiredService<IMatchRepository>();
        await repository.SaveMatchAsync(message.MatchId, message.UserIds, cancellationToken);

        logger.LogInformation("Processed match completion: {MatchId} with {PlayerCount} players",
            message.MatchId, message.UserIds.Length);
    }
}
