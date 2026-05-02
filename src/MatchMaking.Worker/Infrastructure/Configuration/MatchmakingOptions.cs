namespace MatchMaking.Worker.Infrastructure.Configuration;

public sealed class MatchmakingOptions
{
    public const string SectionName = "Matchmaking";
    public required int PlayersPerMatch { get; init; }
}
