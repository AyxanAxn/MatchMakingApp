using MatchMaking.Worker.Application.Abstractions;
using MatchMaking.Worker.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MatchMaking.Worker.Infrastructure.Redis;

public sealed class RedisPlayerQueue(
    IConnectionMultiplexer redis,
    IOptions<MatchmakingOptions> options,
    ILogger<RedisPlayerQueue> logger) : IPlayerQueue
{
    private readonly int _batchSize = options.Value.PlayersPerMatch;

    private const string QueueKeyName = "matchmaking:queue";
    private static readonly RedisKey QueueKey = new(QueueKeyName);

    public async Task<string[]?> AddAndTryPopBatchAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = redis.GetDatabase();

        try
        {
            await db.ListRightPushAsync(QueueKey, userId);
            logger.LogDebug("Player {UserId} added to queue", userId);

            var popped = await db.ListLeftPopAsync(QueueKey, _batchSize);

            if (popped is null || popped.Length < _batchSize)
            {
                if (popped is { Length: > 0 })
                {
                    await db.ListLeftPushAsync(QueueKey, popped);
                    logger.LogDebug("Not enough players ({Count}/{BatchSize}), pushed back to queue",
                        popped.Length, _batchSize);
                }

                return null;
            }

            var players = popped.Select(v => (string)v!).ToArray();
            logger.LogInformation("Popped {Count} players from queue for match", players.Length);

            return players;
        }
        catch (RedisException ex)
        {
            logger.LogError(ex, "Failed to add player {UserId} to queue", userId);
            throw;
        }
    }

    public async Task ReAddPlayersAsync(string[] userIds, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = redis.GetDatabase();

        try
        {
            var values = userIds.Select(id => (RedisValue)id).ToArray();
            await db.ListLeftPushAsync(QueueKey, values);

            logger.LogInformation("Re-added {Count} players to queue after publish failure", userIds.Length);
        }
        catch (RedisException ex)
        {
            logger.LogError(ex, "Failed to re-add {Count} players to queue — players lost: {Players}",
                userIds.Length, string.Join(", ", userIds));
            throw;
        }
    }
}
