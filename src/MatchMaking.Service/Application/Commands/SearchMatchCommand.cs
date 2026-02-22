using MediatR;

namespace MatchMaking.Service.Application.Commands;

public sealed record SearchMatchCommand(string UserId) : IRequest<SearchMatchResult>;

public enum SearchMatchResult
{
    Queued,
    AlreadyInQueue
}
