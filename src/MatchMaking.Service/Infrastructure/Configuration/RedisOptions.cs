namespace MatchMaking.Service.Infrastructure.Configuration;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";
    public required string ConnectionString { get; init; }
    public required int MatchTtlMinutes { get; init; }
}
