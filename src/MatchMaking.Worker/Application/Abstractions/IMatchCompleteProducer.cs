namespace MatchMaking.Worker.Application.Abstractions;

public interface IMatchCompleteProducer
{
    Task PublishMatchCompleteAsync(string matchId, string[] userIds, CancellationToken cancellationToken = default);
}
