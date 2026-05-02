using MediatR;

namespace MatchMaking.Worker.Application.Commands;

public sealed record AccumulatePlayerCommand(string UserId) : IRequest;