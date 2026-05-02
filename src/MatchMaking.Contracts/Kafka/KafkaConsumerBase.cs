using System.Text.Json;
using Confluent.Kafka;
using MatchMaking.Contracts.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MatchMaking.Contracts.Kafka;

public abstract class KafkaConsumerBase<TMessage>(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> kafkaOptions,
    ILogger logger) : BackgroundService
{
    protected abstract string Topic { get; }

    protected abstract Task ProcessMessageAsync(TMessage message, IServiceScope scope, CancellationToken cancellationToken);

    protected virtual bool IsValidMessage(TMessage? message) => message is not null;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        var options = kafkaOptions.Value;
        var config = new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = options.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(Topic);

        logger.LogInformation("Started consuming from {Topic}", Topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var message = JsonSerializer.Deserialize<TMessage>(result.Message.Value);

                if (!IsValidMessage(message))
                {
                    logger.LogWarning("Received invalid message from {Topic}", Topic);
                    consumer.Commit(result);
                    continue;
                }

                using var scope = scopeFactory.CreateScope();
                await ProcessMessageAsync(message!, scope, stoppingToken);

                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Error consuming from {Topic}", Topic);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while processing message from {Topic}", Topic);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        consumer.Close();
    }
}
