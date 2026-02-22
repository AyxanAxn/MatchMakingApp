using MatchMaking.Service.Application.Abstractions;
using MatchMaking.Service.Application.Queries;
using MatchMaking.Service.Domain.Models;
using NSubstitute;

namespace MatchMaking.Service.Tests;

public class GetMatchQueryHandlerTests
{
    private readonly IMatchRepository _repository = Substitute.For<IMatchRepository>();
    private readonly GetMatchQueryHandler _handler;

    public GetMatchQueryHandlerTests()
    {
        _handler = new GetMatchQueryHandler(_repository);
    }

    [Fact]
    public async Task Handle_MatchExists_ReturnsMatchInfo()
    {
        var expected = new MatchInfo("match-123", ["player1", "player2", "player3"]);

        _repository.GetMatchByUserIdAsync("player1", Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _handler.Handle(new GetMatchQuery("player1"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("match-123", result.MatchId);
        Assert.Equal(3, result.UserIds.Length);
        Assert.Contains("player1", result.UserIds);
    }

    [Fact]
    public async Task Handle_NoMatch_ReturnsNull()
    {
        _repository.GetMatchByUserIdAsync("unknown", Arg.Any<CancellationToken>())
            .Returns((MatchInfo?)null);

        var result = await _handler.Handle(new GetMatchQuery("unknown"), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        _repository.GetMatchByUserIdAsync("player1", Arg.Any<CancellationToken>())
            .Returns((MatchInfo?)null);

        await _handler.Handle(new GetMatchQuery("player1"), cts.Token);

        await _repository.Received(1)
            .GetMatchByUserIdAsync("player1", cts.Token);
    }
}
