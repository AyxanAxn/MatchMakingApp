using System.Text.Json;
using MatchMaking.Service.Application.Abstractions;
using MatchMaking.Service.Domain.Models;
using MatchMaking.Service.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MatchMaking.Service.Infrastructure.Redis;

public sealed class RedisMatchRepository : IMatchRepository
{
    private const string MatchKeyPrefix = "match:user:";
    private const string QueueKeyName = "matchmaking:queue";

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisMatchRepository> _logger;
    private readonly TimeSpan _matchTtl;

    public RedisMatchRepository(
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> redisOptions,
        ILogger<RedisMatchRepository> logger)
    {
        _redis = redis;
        _logger = logger;
        _matchTtl = TimeSpan.FromMinutes(redisOptions.Value.MatchTtlMinutes);
    }

    public async Task<MatchInfo?> GetMatchByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync($"{MatchKeyPrefix}{userId}");

        if (value.IsNullOrEmpty)
            return null;

        var match = JsonSerializer.Deserialize<MatchInfo>(value!);

        if (match is null)
        {
            _logger.LogWarning("Failed to deserialize match data for user {UserId}", userId);
            return null;
        }

        return match;
    }

    public async Task SaveMatchAsync(string matchId, string[] userIds, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var matchInfo = new MatchInfo(matchId, userIds);
        var json = JsonSerializer.Serialize(matchInfo);

        var batch = db.CreateBatch();
        var tasks = new List<Task>();

        foreach (var userId in userIds)
        {
            tasks.Add(batch.StringSetAsync($"{MatchKeyPrefix}{userId}", json, _matchTtl));
        }

        batch.Execute();
        await Task.WhenAll(tasks);

        _logger.LogInformation("Saved match {MatchId} for {PlayerCount} players", matchId, userIds.Length);
    }

    public async Task<bool> IsPlayerInQueueAsync(string userId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var position = await db.ListPositionAsync(QueueKeyName, userId);
        return position >= 0;
    }
}
