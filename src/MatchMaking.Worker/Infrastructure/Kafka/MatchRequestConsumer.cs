using System.Text.Json;
using Confluent.Kafka;
using MatchMaking.Contracts.Constants;
using MatchMaking.Contracts.Messages;
using MatchMaking.Worker.Application.Commands;
using MatchMaking.Worker.Infrastructure.Configuration;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MatchMaking.Worker.Infrastructure.Kafka;

// Picks up "I want to play" requests from Kafka and adds players to the Redis queue.
// When enough players are in the queue, a match is formed automatically.
// 2 instances of this worker run in parallel — Kafka splits messages between them.
public sealed class MatchRequestConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _kafkaOptions;
    private readonly ILogger<MatchRequestConsumer> _logger;

    public MatchRequestConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<MatchRequestConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _kafkaOptions = kafkaOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the host finish starting before we block on Kafka
        await Task.Yield();

        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaOptions.BootstrapServers,
            // Both worker instances share this group — Kafka assigns each a partition
            GroupId = _kafkaOptions.ConsumerGroupId,
            // Start from the beginning so we don't miss requests on first launch
            AutoOffsetReset = AutoOffsetReset.Earliest,
            // We commit manually after processing — no data loss if we crash
            EnableAutoCommit = false,
            // Auto-create the topic if it doesn't exist yet on first startup
            AllowAutoCreateTopics = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.MatchmakingRequest);

        _logger.LogInformation("Started consuming from {Topic}", KafkaTopics.MatchmakingRequest);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var request = JsonSerializer.Deserialize<MatchmakingRequest>(result.Message.Value);

                if (request is null || string.IsNullOrWhiteSpace(request.UserId))
                {
                    _logger.LogWarning("Received invalid matchmaking request message");
                    consumer.Commit(result);
                    continue;
                }

                // Add player to Redis queue — if enough players, a match forms instantly
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(new AccumulatePlayerCommand(request.UserId), stoppingToken);

                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Error consuming from {Topic}", KafkaTopics.MatchmakingRequest);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while processing matchmaking request");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        consumer.Close();
    }
}