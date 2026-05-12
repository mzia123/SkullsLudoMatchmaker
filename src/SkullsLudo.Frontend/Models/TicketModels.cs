using System.ComponentModel.DataAnnotations;

namespace SkullsLudo.Frontend.Models;

public sealed class CreateTicketRequest
{
    [Required]
    public required string PlayerId { get; init; }

    [Range(0, double.MaxValue)]
    public required double Mmr { get; init; }

    [Required]
    [RegularExpression("^(practice|quickplay)$", ErrorMessage = "Queue must be 'practice' or 'quickplay'.")]
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
