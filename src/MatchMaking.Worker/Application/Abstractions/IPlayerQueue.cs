namespace MatchMaking.Worker.Application.Abstractions;

public interface IPlayerQueue
{
    Task<string[]?> AddAndTryPopBatchAsync(string userId, CancellationToken cancellationToken = default);
    Task ReAddPlayersAsync(string[] userIds, CancellationToken cancellationToken = default);
}
