using OpenMatch;
using SkullsLudo.Shared.Constants;

namespace SkullsLudo.MatchFunction.Strategies;

/// <summary>
/// Scores match proposals for the Evaluator's de-collision logic.
/// Higher scores win when two proposals share a ticket.
/// </summary>
public static class MatchScoring
{
    public static double Calculate(IReadOnlyList<Ticket> tickets)
    {
        var baseScore = tickets.Count switch
        {
            >= 4 => 1.0,
            3 => 0.6,
            2 => 0.3,
            _ => 0.1
        };
        // Cap MMR bonus so full lobbies are not consistently outranked by tight 2-player MMR pairs.
        return baseScore + Math.Min(0.2, MmrProximityBonus(tickets));
    }

    private static double MmrProximityBonus(IReadOnlyList<Ticket> tickets)
    {
        if (tickets.Count < 2)
            return 0;

        var mmrs = tickets
            .Select(t => t.SearchFields.DoubleArgs.GetValueOrDefault(WellKnown.SearchFields.Mmr, 0))
            .ToList();

        var mean = mmrs.Average();
        var variance = mmrs.Sum(m => (m - mean) * (m - mean)) / mmrs.Count;
        return 1.0 / (1.0 + variance);
    }
}
