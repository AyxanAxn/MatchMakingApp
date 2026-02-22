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

    public AccumulatePlayerCommandHandlerTests()
    {
        _handler = new AccumulatePlayerCommandHandler(_playerQueue, _producer, _logger);
    }

    [Fact]
    public async Task Handle_NotEnoughPlayers_DoesNotPublishMatch()
    {
        // Queue returns null = not enough players yet
        _playerQueue.AddAndTryPopBatchAsync("player1", Arg.Any<CancellationToken>())
            .Returns((string[]?)null);

        await _handler.Handle(new AccumulatePlayerCommand("player1"), CancellationToken.None);

        // Should NOT publish anything — still waiting for more players
        await _producer.DidNotReceive()
            .PublishMatchCompleteAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EnoughPlayers_PublishesMatchWithAllPlayers()
    {
        var players = new[] { "player1", "player2", "player3" };

        // Queue returns 3 players = match is ready
        _playerQueue.AddAndTryPopBatchAsync("player3", Arg.Any<CancellationToken>())
            .Returns(players);

        await _handler.Handle(new AccumulatePlayerCommand("player3"), CancellationToken.None);

        // Should publish exactly once with all 3 players
        await _producer.Received(1)
            .PublishMatchCompleteAsync(Arg.Any<string>(), players, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EnoughPlayers_GeneratesGuidMatchId()
    {
        var players = new[] { "player1", "player2", "player3" };

        // Capture the matchId when the producer is called
        string? capturedMatchId = null;
        _playerQueue.AddAndTryPopBatchAsync("player3", Arg.Any<CancellationToken>())
            .Returns(players);
        _producer.PublishMatchCompleteAsync(Arg.Do<string>(id => capturedMatchId = id), Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _handler.Handle(new AccumulatePlayerCommand("player3"), CancellationToken.None);

        // Verify the matchId is a valid GUID
        Assert.NotNull(capturedMatchId);
        Assert.True(Guid.TryParse(capturedMatchId, out _));
    }

    [Fact]
    public async Task Handle_QueueThrows_ExceptionPropagates()
    {
        _playerQueue.AddAndTryPopBatchAsync("player1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Redis down"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(new AccumulatePlayerCommand("player1"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ProducerThrows_ExceptionPropagates()
    {
        var players = new[] { "player1", "player2", "player3" };

        _playerQueue.AddAndTryPopBatchAsync("player3", Arg.Any<CancellationToken>())
            .Returns(players);

        _producer.PublishMatchCompleteAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Kafka down"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(new AccumulatePlayerCommand("player3"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ProducerThrows_ReAddsPlayersToQueue()
    {
        var players = new[] { "player1", "player2", "player3" };

        _playerQueue.AddAndTryPopBatchAsync("player3", Arg.Any<CancellationToken>())
            .Returns(players);

        _producer.PublishMatchCompleteAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Kafka down"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(new AccumulatePlayerCommand("player3"), CancellationToken.None));

        // Players should be re-added to the queue so they aren't lost
        await _playerQueue.Received(1)
            .ReAddPlayersAsync(players, Arg.Any<CancellationToken>());
    }
}
