using OpenMatch;
using SkullsLudo.Shared.Constants;

namespace SkullsLudo.MatchFunction.Strategies;

public static class MatchScoring
{
    private static readonly Dictionary<int, double> PlayerCountBaseScores = new()
    {
        { 4, 1.0 },
        { 3, 0.6 },
        { 2, 0.3 }
    };

    public static double Calculate(IReadOnlyList<Ticket> tickets)
    {
        var baseScore = PlayerCountBaseScores.GetValueOrDefault(tickets.Count, 0.1);
        var mmrBonus = CalculateMmrProximityBonus(tickets);
        return baseScore + mmrBonus;
    }

    private static double CalculateMmrProximityBonus(IReadOnlyList<Ticket> tickets)
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
