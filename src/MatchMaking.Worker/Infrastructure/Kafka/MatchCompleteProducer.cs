using System.Text.Json;
using Confluent.Kafka;
using MatchMaking.Contracts.Constants;
using MatchMaking.Contracts.Messages;
using MatchMaking.Worker.Application.Abstractions;
using MatchMaking.Worker.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace MatchMaking.Worker.Infrastructure.Kafka;

// Sends "match is ready" messages back to Kafka so the Service can save them to Redis.
public sealed class MatchCompleteProducer : IMatchCompleteProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<MatchCompleteProducer> _logger;

    public MatchCompleteProducer(IOptions<KafkaOptions> options, ILogger<MatchCompleteProducer> logger)
    {
        _logger = logger;
        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            // Wait for ALL brokers to confirm — safest delivery guarantee
            Acks = Acks.All,
            // Prevents duplicate messages if a network retry happens
            EnableIdempotence = true
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishMatchCompleteAsync(string matchId, string[] userIds, CancellationToken cancellationToken = default)
    {
        var message = new MatchmakingComplete(matchId, userIds);
        var json = JsonSerializer.Serialize(message);

        try
        {
            // Key = matchId so all players in the same match land on the same partition
            await _producer.ProduceAsync(
                KafkaTopics.MatchmakingComplete,
                new Message<string, string> { Key = matchId, Value = json },
                cancellationToken);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Failed to publish match complete for match {MatchId}", matchId);
            throw;
        }
    }
    public void Dispose() => _producer.Dispose();
}