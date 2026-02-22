namespace MatchMaking.Service.Application.Abstractions;

public interface IMatchmakingProducer
{
    Task PublishSearchRequestAsync(string userId, CancellationToken cancellationToken = default);
}
