namespace SkullsLudo.Shared.Configuration;

public sealed class QueueConfiguration
{
    public required string Name { get; init; }
    public required string Tag { get; init; }
    public required int MaxPlayers { get; init; }
    public required int MinPlayers { get; init; }
    public required TimeSpan Timeout { get; init; }
    public List<DegradationStep> DegradationSteps { get; init; } = [];
}

public sealed class DegradationStep
{
    public required TimeSpan After { get; init; }
    public required int PlayerCount { get; init; }
}
