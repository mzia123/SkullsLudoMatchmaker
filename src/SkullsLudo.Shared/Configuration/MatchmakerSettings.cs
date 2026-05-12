namespace SkullsLudo.Shared.Configuration;

public sealed class MatchmakerSettings
{
    public const string SectionName = "Matchmaker";

    public required OpenMatchSettings OpenMatch { get; init; }
    public required AgonesSettings Agones { get; init; }
    public required MatchFunctionSettings MatchFunction { get; init; }
    public required DirectorSettings Director { get; init; }
    public required Dictionary<string, QueueConfiguration> Queues { get; init; }
}

public sealed class OpenMatchSettings
{
    public required string FrontendHost { get; init; }
    public int FrontendPort { get; init; } = 50504;
    public required string BackendHost { get; init; }
    public int BackendPort { get; init; } = 50505;
    public required string QueryHost { get; init; }
    public int QueryPort { get; init; } = 50503;
}

public sealed class AgonesSettings
{
    public required string AllocatorHost { get; init; }
    public int AllocatorPort { get; init; } = 443;
    public string? ClientCertPath { get; init; }
    public string? ClientKeyPath { get; init; }
    public string? ServerCaPath { get; init; }
}

public sealed class MatchFunctionSettings
{
    public required string Host { get; init; }
    public int Port { get; init; } = 50502;
}

public sealed class DirectorSettings
{
    public int LoopIntervalMs { get; init; } = 5000;
}
