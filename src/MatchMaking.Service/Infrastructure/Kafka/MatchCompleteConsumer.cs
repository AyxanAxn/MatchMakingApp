using System.Text.Json;
using Confluent.Kafka;
using MatchMaking.Contracts.Constants;
using MatchMaking.Contracts.Messages;
using MatchMaking.Service.Application.Abstractions;
using MatchMaking.Service.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MatchMaking.Service.Infrastructure.Kafka;

// Listens for "match is ready" messages from the Worker and saves them to Redis.
// Runs as a background service — starts when the app starts, loops forever.
public sealed class MatchCompleteConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _kafkaOptions;
    private readonly ILogger<MatchCompleteConsumer> _logger;

    public MatchCompleteConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<MatchCompleteConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _kafkaOptions = kafkaOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the rest of the app finish starting before we begin consuming
        await Task.Yield();

        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaOptions.BootstrapServers,
            GroupId = _kafkaOptions.ConsumerGroupId,
            // Start reading from the beginning so we don't miss anything on first launch
            AutoOffsetReset = AutoOffsetReset.Earliest,
            // We commit manually after saving to Redis — no data loss if we crash mid-process
            EnableAutoCommit = false,
            // Auto-create the topic if it doesn't exist yet on first startup
            AllowAutoCreateTopics = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.MatchmakingComplete);

        _logger.LogInformation("Started consuming from {Topic}", KafkaTopics.MatchmakingComplete);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var matchComplete = JsonSerializer.Deserialize<MatchmakingComplete>(result.Message.Value);

                if (matchComplete is null)
                {
                    _logger.LogWarning("Received null message from {Topic}", KafkaTopics.MatchmakingComplete);
                    consumer.Commit(result);
                    continue;
                }

                // Save the match to Redis so the GET /match/{userId} endpoint can return it
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IMatchRepository>();
                await repository.SaveMatchAsync(matchComplete.MatchId, matchComplete.UserIds, stoppingToken);

                // Only commit after saving — if we crash before this, Kafka will redeliver the message
                consumer.Commit(result);

                _logger.LogInformation("Processed match completion: {MatchId} with {PlayerCount} players",
                    matchComplete.MatchId, matchComplete.UserIds.Length);
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Error consuming from {Topic}", KafkaTopics.MatchmakingComplete);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while processing match completion");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        consumer.Close();
    }
}
