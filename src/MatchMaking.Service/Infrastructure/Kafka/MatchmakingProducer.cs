using System.Text.Json;
using Confluent.Kafka;
using MatchMaking.Contracts.Constants;
using MatchMaking.Contracts.Messages;
using MatchMaking.Service.Application.Abstractions;
using MatchMaking.Service.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MatchMaking.Service.Infrastructure.Kafka;

// Sends "I want to play" requests to Kafka so the Worker can pick them up.
public sealed class MatchmakingProducer : IMatchmakingProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<MatchmakingProducer> _logger;

    public MatchmakingProducer(IOptions<KafkaOptions> options, ILogger<MatchmakingProducer> logger)
    {
        _logger = logger;
        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            // Wait for ALL brokers to confirm the write — safest delivery guarantee
            Acks = Acks.All,
            // Prevents duplicate messages if a network retry happens
            EnableIdempotence = true
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishSearchRequestAsync(string userId, CancellationToken cancellationToken = default)
    {
        var message = new MatchmakingRequest(userId);
        var json = JsonSerializer.Serialize(message);

        try
        {
            // Key = userId so all messages from the same user go to the same Kafka partition
            await _producer.ProduceAsync(
                KafkaTopics.MatchmakingRequest,
                new Message<string, string> { Key = userId, Value = json },
                cancellationToken);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Failed to publish matchmaking request for user {UserId}", userId);
            throw;
        }
    }

    public void Dispose() => _producer.Dispose();
}
