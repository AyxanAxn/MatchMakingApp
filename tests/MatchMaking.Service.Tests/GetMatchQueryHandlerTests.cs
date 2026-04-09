using AutoFixture;
using MatchMaking.Service.Application.Abstractions;
using MatchMaking.Service.Application.Queries;
using MatchMaking.Service.Domain.Models;
using NSubstitute;

namespace MatchMaking.Service.Tests;

public class GetMatchQueryHandlerTests
{
    private readonly IMatchRepository _repository = Substitute.For<IMatchRepository>();
    private readonly GetMatchQueryHandler _handler;
    private readonly Fixture _fixture = new();

    public GetMatchQueryHandlerTests()
    {
        _handler = new GetMatchQueryHandler(_repository);
    }

    [Fact]
    public async Task Handle_MatchExists_ReturnsMatchInfo()
    {
        var userId = _fixture.Create<string>();
        var expected = new MatchInfo(_fixture.Create<string>(), [userId, _fixture.Create<string>(), _fixture.Create<string>()]);

        _repository.GetMatchByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _handler.Handle(new GetMatchQuery(userId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expected.MatchId, result.MatchId);
        Assert.Equal(expected.UserIds.Length, result.UserIds.Length);
        Assert.Contains(userId, result.UserIds);
    }

    [Fact]
    public async Task Handle_NoMatch_ReturnsNull()
    {
        var userId = _fixture.Create<string>();

        _repository.GetMatchByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((MatchInfo?)null);

        var result = await _handler.Handle(new GetMatchQuery(userId), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        var userId = _fixture.Create<string>();
        using var cts = new CancellationTokenSource();

        _repository.GetMatchByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((MatchInfo?)null);

        await _handler.Handle(new GetMatchQuery(userId), cts.Token);

        await _repository.Received(1)
            .GetMatchByUserIdAsync(userId, cts.Token);
    }
}
