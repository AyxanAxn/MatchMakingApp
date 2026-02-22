namespace MatchMaking.Service.Infrastructure.Configuration;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiter";
    public const string PolicyName = "matchmaking";
    public required int WindowMs { get; init; }
    public required int PermitLimit { get; init; }
    public required int QueueLimit { get; init; }
}
