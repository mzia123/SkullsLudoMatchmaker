namespace SkullsLudo.Shared.Configuration;

public sealed class MatchmakerSettings
{
    public const string SectionName = "Matchmaker";

    public OpenMatchSettings OpenMatch { get; init; } = new();
    public AgonesSettings Agones { get; init; } = new();
    public MatchFunctionSettings MatchFunction { get; init; } = new();
    public DirectorSettings Director { get; init; } = new();
    public Dictionary<string, QueueConfiguration> Queues { get; init; } = new();
}

public sealed class OpenMatchSettings
{
    public string FrontendHost { get; init; } = "open-match-frontend.open-match.svc.cluster.local";
    public int FrontendPort { get; init; } = 50504;
    public string BackendHost { get; init; } = "open-match-backend.open-match.svc.cluster.local";
    public int BackendPort { get; init; } = 50505;
    public string QueryHost { get; init; } = "open-match-query.open-match.svc.cluster.local";
    public int QueryPort { get; init; } = 50503;
}

public sealed class AgonesSettings
{
    public string AllocatorHost { get; init; } = "agones-allocator.agones-system.svc.cluster.local";
    public int AllocatorPort { get; init; } = 443;
    public string Namespace { get; init; } = "default";
    public string FleetName { get; init; } = "ludo-prod-fleet";

    public string ClientCertPath { get; init; } = "/agones/certs/tls.crt";
    public string ClientKeyPath { get; init; } = "/agones/certs/tls.key";
    public string ServerCaPath { get; init; } = "/agones/ca/ca.crt";

    public int AllocationTimeoutSeconds { get; init; } = 5;

    public string AnnotationPrefix { get; init; } = "sl";
}

public sealed class MatchFunctionSettings
{
    public string Host { get; init; } = "skulls-ludo-matchfunction.default.svc.cluster.local";
    public int Port { get; init; } = 50502;
}

public sealed class DirectorSettings
{
    public int LoopIntervalMs { get; init; } = 5000;
}
