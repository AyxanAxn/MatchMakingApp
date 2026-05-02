using MatchMaking.Service.Domain.Models;
using MediatR;

namespace MatchMaking.Service.Application.Queries;

public sealed record GetMatchQuery(string UserId) : IRequest<MatchInfo?>;
