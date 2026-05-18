using System.ComponentModel.DataAnnotations;

namespace SkullsLudo.Frontend.Models;

public sealed class CreateTicketRequest
{
    [Range(0, 100_000)]
    public required double Mmr { get; init; }

    /// <summary>
    /// Queue identifier (e.g. <c>classic-team</c>, <c>ranked</c>). Validated at
    /// runtime against the configured <c>Matchmaker:Queues</c> dictionary.
    /// </summary>
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public required string Queue { get; init; }
}

public sealed class CreateTicketResponse
{
    public required string TicketId { get; init; }
}

public sealed class TicketStatusResponse
{
    public required string TicketId { get; init; }
    public required string Status { get; init; }
    public string? Connection { get; init; }
    public int? PlayerCount { get; init; }
}

public static class TicketStatus
{
    public const string Searching = "searching";
    public const string Matched = "matched";
    public const string Timeout = "timeout";
}
