namespace SkullsLudo.Shared.Configuration;

public sealed class QueueConfiguration
{
    public string Name { get; init; } = string.Empty;
    public string Tag { get; init; } = string.Empty;
    public int MaxPlayers { get; init; }
    public int MinPlayers { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);
    public List<DegradationStep> DegradationSteps { get; init; } = [];
}

public sealed class DegradationStep
{
    public TimeSpan After { get; init; }
    public int PlayerCount { get; init; }
}
