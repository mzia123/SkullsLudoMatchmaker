namespace SkullsLudo.Shared.Configuration;

public sealed class QueueConfiguration
{
    public string Name { get; init; } = string.Empty;
    public string Tag { get; init; } = string.Empty;

    /// <summary>
    /// Name of the <c>IMatchStrategy</c> that processes this queue. Multiple queues
    /// can target the same strategy with different parameters (e.g. MaxPlayers,
    /// DegradationSteps, MMR range).
    /// </summary>
    public string Strategy { get; init; } = string.Empty;

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
