using Google.Protobuf.WellKnownTypes;
using OpenMatch;
using SkullsLudo.Shared.Configuration;
using SkullsLudo.Shared.Constants;

namespace SkullsLudo.MatchFunction.Strategies;

/// <summary>
/// MMR-proximity grouping with degrading target sizes. Starts at
/// <see cref="QueueConfiguration.MaxPlayers"/>, then falls back through each
/// <see cref="QueueConfiguration.DegradationSteps"/> entry as tickets age.
/// Stateless and reusable across any multi-player queue (quickplay, ranked, ...).
/// </summary>
public sealed class DegradingMmrMatchStrategy : IMatchStrategy
{
    public string Name => WellKnown.Strategies.DegradingMmr;

    public IReadOnlyList<Match> CreateMatches(
        MatchProfile profile,
        IDictionary<string, IList<Ticket>> poolTickets,
        QueueConfiguration queueConfig)
    {
        var sortedByMmr = poolTickets.Values.SelectMany(t => t)
            .OrderBy(t => t.SearchFields.DoubleArgs.GetValueOrDefault(WellKnown.SearchFields.Mmr, 0))
            .ToList();

        var now = DateTime.UtcNow;
        var used = new HashSet<string>();
        var matches = new List<Match>();

        foreach (var (requiredAge, playerCount) in BuildTargetSizes(queueConfig))
        {
            var eligible = sortedByMmr
                .Where(t => !used.Contains(t.Id) && TicketAge(t, now) >= requiredAge)
                .ToList();

            foreach (var group in GroupByMmrProximity(eligible, playerCount))
            {
                var match = new Match
                {
                    MatchId = $"{queueConfig.Name}-{playerCount}p-{Guid.NewGuid():N}",
                    MatchProfile = profile.Name,
                    MatchFunction = Name,
                    Tickets = { group }
                };
                match.Extensions[WellKnown.Extensions.ScoreKey] = Any.Pack(new DoubleValue { Value = MatchScoring.Calculate(group) });
                match.Extensions[WellKnown.Extensions.PlayerCountKey] = Any.Pack(new Int32Value { Value = group.Count });
                match.Extensions[WellKnown.Extensions.QueueKey] = Any.Pack(new StringValue { Value = queueConfig.Name });

                matches.Add(match);
                foreach (var t in group) used.Add(t.Id);
            }
        }

        return matches;
    }

    private static List<(TimeSpan RequiredAge, int PlayerCount)> BuildTargetSizes(QueueConfiguration config)
    {
        var targets = new List<(TimeSpan, int)> { (TimeSpan.Zero, config.MaxPlayers) };

        foreach (var step in config.DegradationSteps.OrderBy(s => s.After))
            targets.Add((step.After, step.PlayerCount));

        return targets;
    }

    private static List<List<Ticket>> GroupByMmrProximity(List<Ticket> eligible, int groupSize)
    {
        if (eligible.Count < groupSize)
            return [];

        var results = new List<List<Ticket>>();
        var usedInPass = new HashSet<string>();

        for (var i = 0; i <= eligible.Count - groupSize; i++)
        {
            if (usedInPass.Contains(eligible[i].Id))
                continue;

            var group = new List<Ticket>();
            for (var j = i; j < eligible.Count && group.Count < groupSize; j++)
            {
                if (!usedInPass.Contains(eligible[j].Id))
                    group.Add(eligible[j]);
            }

            if (group.Count == groupSize)
            {
                results.Add(group);
                foreach (var t in group) usedInPass.Add(t.Id);
            }
        }

        return results;
    }

    private static TimeSpan TicketAge(Ticket ticket, DateTime now)
    {
        if (ticket.CreateTime is null) return TimeSpan.Zero;
        var age = now - ticket.CreateTime.ToDateTime();
        return age > TimeSpan.Zero ? age : TimeSpan.Zero;
    }
}
