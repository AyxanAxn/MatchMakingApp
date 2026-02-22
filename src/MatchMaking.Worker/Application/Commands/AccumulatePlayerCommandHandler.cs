using MatchMaking.Worker.Application.Abstractions;
using MediatR;

namespace MatchMaking.Worker.Application.Commands;

public sealed class AccumulatePlayerCommandHandler(
    IPlayerQueue playerQueue,
    IMatchCompleteProducer producer,
    ILogger<AccumulatePlayerCommandHandler> logger) : IRequestHandler<AccumulatePlayerCommand>
{
    private readonly IPlayerQueue _playerQueue = playerQueue;
    private readonly IMatchCompleteProducer _producer = producer;
    private readonly ILogger<AccumulatePlayerCommandHandler> _logger = logger;

    public async Task Handle(AccumulatePlayerCommand request, CancellationToken cancellationToken)
    {
        var players = await _playerQueue.AddAndTryPopBatchAsync(request.UserId, cancellationToken);

        if (players is null)
            return;

        var matchId = Guid.NewGuid().ToString();

        try
        {
            await _producer.PublishMatchCompleteAsync(matchId, players, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish match {MatchId}, re-queuing {Count} players",
                matchId, players.Length);
            await _playerQueue.ReAddPlayersAsync(players, cancellationToken);
            throw;
        }

        _logger.LogInformation("Match completed: {MatchId} with players: {Players}",
            matchId, string.Join(", ", players));
    }
}
