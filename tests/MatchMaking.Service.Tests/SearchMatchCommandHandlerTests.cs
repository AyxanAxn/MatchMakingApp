using AutoFixture;
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
    private readonly Fixture _fixture = new();

    public SearchMatchCommandHandlerTests()
    {
        _handler = new SearchMatchCommandHandler(_producer, _repository, _logger);
    }

    [Fact]
    public async Task Handle_ValidUserId_ReturnsQueued()
    {
        var userId = _fixture.Create<string>();

        _repository.IsPlayerInQueueAsync(userId, Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _handler.Handle(new SearchMatchCommand(userId), CancellationToken.None);

        Assert.Equal(SearchMatchResult.Queued, result);
    }

    [Fact]
    public async Task Handle_ValidUserId_PublishesToKafka()
    {
        var userId = _fixture.Create<string>();

        _repository.IsPlayerInQueueAsync(userId, Arg.Any<CancellationToken>())
            .Returns(false);

        await _handler.Handle(new SearchMatchCommand(userId), CancellationToken.None);

        await _producer.Received(1)
            .PublishSearchRequestAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PlayerAlreadyInQueue_ReturnsAlreadyInQueue()
    {
        var userId = _fixture.Create<string>();

        _repository.IsPlayerInQueueAsync(userId, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _handler.Handle(new SearchMatchCommand(userId), CancellationToken.None);

        Assert.Equal(SearchMatchResult.AlreadyInQueue, result);
        await _producer.DidNotReceive()
            .PublishSearchRequestAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ProducerThrows_ExceptionPropagates()
    {
        var userId = _fixture.Create<string>();

        _repository.IsPlayerInQueueAsync(userId, Arg.Any<CancellationToken>())
            .Returns(false);

        _producer.PublishSearchRequestAsync(userId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Kafka down"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(new SearchMatchCommand(userId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        var userId = _fixture.Create<string>();
        using var cts = new CancellationTokenSource();

        _repository.IsPlayerInQueueAsync(userId, cts.Token)
            .Returns(false);

        await _handler.Handle(new SearchMatchCommand(userId), cts.Token);

        await _producer.Received(1)
            .PublishSearchRequestAsync(userId, cts.Token);
    }
}
