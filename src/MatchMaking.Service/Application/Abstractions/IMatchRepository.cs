using MatchMaking.Service.Domain.Models;

namespace MatchMaking.Service.Application.Abstractions;

public interface IMatchRepository
{
    Task<MatchInfo?> GetMatchByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task SaveMatchAsync(string matchId, string[] userIds, CancellationToken cancellationToken = default);
    Task<bool> IsPlayerInQueueAsync(string userId, CancellationToken cancellationToken = default);
}
