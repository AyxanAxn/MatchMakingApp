namespace MatchMaking.Contracts.Messages;

public sealed record MatchmakingComplete(string MatchId, string[] UserIds);
