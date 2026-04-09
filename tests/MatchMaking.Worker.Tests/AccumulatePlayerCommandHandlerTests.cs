using AutoFixture;
using MatchMaking.Worker.Application.Abstractions;
using MatchMaking.Worker.Application.Commands;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MatchMaking.Worker.Tests;

public class AccumulatePlayerCommandHandlerTests
{
    private readonly IPlayerQueue _playerQueue = Substitute.For<IPlayerQueue>();
    private readonly IMatchCompleteProducer _producer = Substitute.For<IMatchCompleteProducer>();
    private readonly ILogger<AccumulatePlayerCommandHandler> _logger = Substitute.For<ILogger<AccumulatePlayerCommandHandler>>();
    private readonly AccumulatePlayerCommandHandler _handler;
    private readonly Fixture _fixture = new();

    public AccumulatePlayerCommandHandlerTests()
    {
        _handler = new AccumulatePlayerCommandHandler(_playerQueue, _producer, _logger);
    }

    [Fact]
    public async Task Handle_NotEnoughPlayers_DoesNotPublishMatch()
    {
        var userId = _fixture.Create<string>();

        _playerQueue.AddAndTryPopBatchAsync(userId, Arg.Any<CancellationToken>())
            .Returns((string[]?)null);

        await _handler.Handle(new AccumulatePlayerCommand(userId), CancellationToken.None);

        await _producer.DidNotReceive()
            .PublishMatchCompleteAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EnoughPlayers_PublishesMatchWithAllPlayers()
    {
        var players = _fixture.CreateMany<string>(3).ToArray();

        _playerQueue.AddAndTryPopBatchAsync(players[2], Arg.Any<CancellationToken>())
            .Returns(players);

        await _handler.Handle(new AccumulatePlayerCommand(players[2]), CancellationToken.None);

        await _producer.Received(1)
            .PublishMatchCompleteAsync(Arg.Any<string>(), players, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EnoughPlayers_GeneratesGuidMatchId()
    {
        var players = _fixture.CreateMany<string>(3).ToArray();

        string? capturedMatchId = null;
        _playerQueue.AddAndTryPopBatchAsync(players[2], Arg.Any<CancellationToken>())
            .Returns(players);
        _producer.PublishMatchCompleteAsync(Arg.Do<string>(id => capturedMatchId = id), Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _handler.Handle(new AccumulatePlayerCommand(players[2]), CancellationToken.None);

        Assert.NotNull(capturedMatchId);
        Assert.True(Guid.TryParse(capturedMatchId, out _));
    }

    [Fact]
    public async Task Handle_QueueThrows_ExceptionPropagates()
    {
        var userId = _fixture.Create<string>();

        _playerQueue.AddAndTryPopBatchAsync(userId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Redis down"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(new AccumulatePlayerCommand(userId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ProducerThrows_ExceptionPropagates()
    {
        var players = _fixture.CreateMany<string>(3).ToArray();

        _playerQueue.AddAndTryPopBatchAsync(players[2], Arg.Any<CancellationToken>())
            .Returns(players);

        _producer.PublishMatchCompleteAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Kafka down"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(new AccumulatePlayerCommand(players[2]), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ProducerThrows_ReAddsPlayersToQueue()
    {
        var players = _fixture.CreateMany<string>(3).ToArray();

        _playerQueue.AddAndTryPopBatchAsync(players[2], Arg.Any<CancellationToken>())
            .Returns(players);

        _producer.PublishMatchCompleteAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Kafka down"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(new AccumulatePlayerCommand(players[2]), CancellationToken.None));

        await _playerQueue.Received(1)
            .ReAddPlayersAsync(players, Arg.Any<CancellationToken>());
    }
}
