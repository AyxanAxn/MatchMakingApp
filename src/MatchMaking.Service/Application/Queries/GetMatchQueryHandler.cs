using MatchMaking.Service.Application.Abstractions;
using MatchMaking.Service.Domain.Models;
using MediatR;

namespace MatchMaking.Service.Application.Queries;

public sealed class GetMatchQueryHandler : IRequestHandler<GetMatchQuery, MatchInfo?>
{
    private readonly IMatchRepository _repository;

    public GetMatchQueryHandler(IMatchRepository repository)
    {
        _repository = repository;
    }

    public async Task<MatchInfo?> Handle(GetMatchQuery request, CancellationToken cancellationToken)
    {
        return await _repository.GetMatchByUserIdAsync(request.UserId, cancellationToken);
    }
}
