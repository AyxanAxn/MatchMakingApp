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

    // This Lua script runs atomically inside Redis, so two workers
    // can never pop the same players — no race conditions possible.
    private const string MatchmakingScript = """
        local queue = KEYS[1]              -- "matchmaking:queue"
        local userId = ARGV[1]             -- the player joining the queue
        local requiredPlayers = tonumber(ARGV[2])  -- e.g. 3

        -- Skip if player is already in the queue (prevent duplicates)
        local pos = redis.call('LPOS', queue, userId)
        if pos then
            return nil
        end

        -- RPUSH = add to end of list (like Enqueue)
        redis.call('RPUSH', queue, userId)

        -- LLEN = get list length (count of waiting players)
        local playerCount = redis.call('LLEN', queue)

        -- Not enough players yet — just wait
        if playerCount < requiredPlayers then
            return nil
        end

        -- LPOP = remove from front of list (like Dequeue)
        local players = {}
        for i = 1, requiredPlayers do
            players[i] = redis.call('LPOP', queue)
        end

        return players
        """;

    private const string QueueKeyName = "matchmaking:queue";
    private static readonly RedisKey QueueKey = new(QueueKeyName);

    public async Task<string[]?> AddAndTryPopBatchAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = redis.GetDatabase();

        try
        {
            var result = await db.ScriptEvaluateAsync(
                MatchmakingScript,
                [QueueKey],
                [userId, _batchSize]);

            if (result.IsNull)
                return null;

            var values = (RedisResult[])result!;
            var players = values
                .Where(v => !v.IsNull)
                .Select(v => (string)v!)
                .ToArray();

            if (players.Length != _batchSize)
            {
                logger.LogWarning(
                    "Expected {BatchSize} players but got {Count} — queue may have been modified externally",
                    _batchSize, players.Length);
                return null;
            }

            return players;
        }
        catch (RedisException ex)
        {
            logger.LogError(ex, "Failed to add player {UserId} to queue", userId);
            throw;
        }
    }

    public async Task ReAddPlayersAsync(string[] userIds, CancellationToken cancellationToken = default)
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
