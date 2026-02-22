using MatchMaking.Service.Application.Abstractions;
using MatchMaking.Service.Application.Commands;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MatchMaking.Service.Tests;

public class SearchMatchCommandHandlerTests
{
    private readonly IMatchmakingProducer _producer = Substitute.For<IMatchmakingProducer>();
    private readonly IMatchRepository _repository = Substitute.For<IMatchRepository>();
    private readonly ILogger<SearchMatchCommandHandler> _logger = Substitute.For<ILogger<SearchMatchCommandHandler>>();
    private readonly SearchMatchCommandHandler _handler;

    public SearchMatchCommandHandlerTests()
    {
        _handler = new SearchMatchCommandHandler(_producer, _repository, _logger);
    }

    [Fact]
    public async Task Handle_ValidUserId_ReturnsQueued()
    {
        _repository.IsPlayerInQueueAsync("player1", Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _handler.Handle(new SearchMatchCommand("player1"), CancellationToken.None);

        Assert.Equal(SearchMatchResult.Queued, result);
    }

    [Fact]
    public async Task Handle_ValidUserId_PublishesToKafka()
    {
        _repository.IsPlayerInQueueAsync("player1", Arg.Any<CancellationToken>())
            .Returns(false);

        await _handler.Handle(new SearchMatchCommand("player1"), CancellationToken.None);

        await _producer.Received(1)
            .PublishSearchRequestAsync("player1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PlayerAlreadyInQueue_ReturnsAlreadyInQueue()
    {
        _repository.IsPlayerInQueueAsync("player1", Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _handler.Handle(new SearchMatchCommand("player1"), CancellationToken.None);

        Assert.Equal(SearchMatchResult.AlreadyInQueue, result);
        await _producer.DidNotReceive()
            .PublishSearchRequestAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ProducerThrows_ExceptionPropagates()
    {
        _repository.IsPlayerInQueueAsync("player1", Arg.Any<CancellationToken>())
            .Returns(false);

        _producer.PublishSearchRequestAsync("player1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Kafka down"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(new SearchMatchCommand("player1"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        _repository.IsPlayerInQueueAsync("player1", cts.Token)
            .Returns(false);

        await _handler.Handle(new SearchMatchCommand("player1"), cts.Token);

        await _producer.Received(1)
            .PublishSearchRequestAsync("player1", cts.Token);
    }
}
