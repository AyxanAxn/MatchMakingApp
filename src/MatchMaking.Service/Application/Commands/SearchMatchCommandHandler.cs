using MatchMaking.Service.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MatchMaking.Service.Application.Commands;

public sealed class SearchMatchCommandHandler(
    IMatchmakingProducer producer,
    IMatchRepository repository,
    ILogger<SearchMatchCommandHandler> logger) : IRequestHandler<SearchMatchCommand, SearchMatchResult>
{
    private readonly IMatchmakingProducer _producer = producer;
    private readonly IMatchRepository _repository = repository;
    private readonly ILogger<SearchMatchCommandHandler> _logger = logger;

    public async Task<SearchMatchResult> Handle(SearchMatchCommand request, CancellationToken cancellationToken)
    {
        if (await _repository.IsPlayerInQueueAsync(request.UserId, cancellationToken))
            return SearchMatchResult.AlreadyInQueue;

        try
        {
            await _producer.PublishSearchRequestAsync(request.UserId, cancellationToken);
            return SearchMatchResult.Queued;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish matchmaking request for user {UserId}", request.UserId);
            throw;
        }
    }
}
