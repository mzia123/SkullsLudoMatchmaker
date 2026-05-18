namespace SkullsLudo.Shared.Configuration;

public sealed class MatchmakerSettings
{
    public const string SectionName = "Matchmaker";

    public OpenMatchSettings OpenMatch { get; init; } = new();
    public AgonesSettings Agones { get; init; } = new();
    public MatchFunctionSettings MatchFunction { get; init; } = new();
    public DirectorSettings Director { get; init; } = new();
    public FrontendSettings Frontend { get; init; } = new();
    public UnityAuthenticationSettings UnityAuth { get; init; } = new();
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

    public int AllocationTimeoutSeconds { get; init; } = 15;

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

public sealed class FrontendSettings
{
    /// <summary>How long a ticket may stay in <c>searching</c> before the API reports timeout.</summary>
    public int TicketSearchTimeoutSeconds { get; init; } = 120;

    /// <summary>Allowed <c>POST /tickets</c> requests per <see cref="RateLimitWindowSeconds"/> per remote IP.</summary>
    public int CreateTicketRateLimitPermits { get; init; } = 1;

    /// <summary>Window size for the create-ticket rate limit (seconds).</summary>
    public int CreateTicketRateLimitWindowSeconds { get; init; } = 10;
}

public sealed class UnityAuthenticationSettings
{
    public bool Enabled { get; init; } = true;

    public string JwksUri { get; init; } = "https://player-auth.services.api.unity.com/.well-known/jwks.json";

    public string ValidIssuer { get; init; } = "https://player-auth.services.api.unity.com";

    public string? ValidAudience { get; init; }

    public string? ValidProjectId { get; init; }

    public int JwksCacheTtlHours { get; init; } = 8;

    public string PlayerIdClaim { get; init; } = "sub";

    public string DebugPlayerIdHeader { get; init; } = "X-Debug-Player-Id";

    public string DefaultDebugPlayerId { get; init; } = "dev-player";
}
