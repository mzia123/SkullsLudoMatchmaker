using Google.Protobuf.WellKnownTypes;
using OpenMatch;
using SkullsLudo.Shared.Configuration;
using SkullsLudo.Shared.Constants;

namespace SkullsLudo.MatchFunction.Strategies;

public sealed class QuickplayMatchStrategy : IMatchStrategy
{
    private readonly QueueConfiguration _config;

    public QuickplayMatchStrategy(QueueConfiguration config)
    {
        _config = config;
    }

    public string QueueName => WellKnown.Queues.Quickplay;

    public IReadOnlyList<Match> CreateMatches(
        MatchProfile profile,
        IDictionary<string, IList<Ticket>> poolTickets)
    {
        var allTickets = poolTickets.Values.SelectMany(t => t).ToList();
        var now = DateTime.UtcNow;
        var used = new HashSet<string>();
        var matches = new List<Match>();

        var sortedByMmr = allTickets
            .OrderBy(t => t.SearchFields.DoubleArgs.GetValueOrDefault(WellKnown.SearchFields.Mmr, 0))
            .ToList();

        var targetSizes = BuildTargetSizes();

        foreach (var (requiredAge, playerCount) in targetSizes)
        {
            var eligible = sortedByMmr
                .Where(t => !used.Contains(t.Id) && TicketAge(t, now) >= requiredAge)
                .ToList();

            var grouped = GroupByMmrProximity(eligible, playerCount);

            foreach (var group in grouped)
            {
                var score = MatchScoring.Calculate(group);

                var match = new Match
                {
                    MatchId = $"quickplay-{playerCount}p-{Guid.NewGuid():N}",
                    MatchProfile = profile.Name,
                    MatchFunction = nameof(QuickplayMatchStrategy)
                };
                match.Tickets.AddRange(group);
                match.Extensions[WellKnown.Extensions.ScoreKey] = Any.Pack(new DoubleValue { Value = score });
                match.Extensions[WellKnown.Extensions.PlayerCountKey] = Any.Pack(new Int32Value { Value = group.Count });
                match.Extensions[WellKnown.Extensions.QueueKey] = Any.Pack(new StringValue { Value = QueueName });

                matches.Add(match);

                foreach (var t in group)
                    used.Add(t.Id);
            }
        }

        return matches;
    }

    private List<(TimeSpan RequiredAge, int PlayerCount)> BuildTargetSizes()
    {
        var targets = new List<(TimeSpan, int)>
        {
            (TimeSpan.Zero, _config.MaxPlayers)
        };

        foreach (var step in _config.DegradationSteps.OrderBy(s => s.After))
        {
            targets.Add((step.After, step.PlayerCount));
        }

        return targets;
    }

    private static List<List<Ticket>> GroupByMmrProximity(List<Ticket> eligible, int groupSize)
    {
        if (eligible.Count < groupSize)
            return [];

        var sorted = eligible
            .OrderBy(t => t.SearchFields.DoubleArgs.GetValueOrDefault(WellKnown.SearchFields.Mmr, 0))
            .ToList();

        var results = new List<List<Ticket>>();
        var usedInPass = new HashSet<string>();

        for (var i = 0; i <= sorted.Count - groupSize; i++)
        {
            if (usedInPass.Contains(sorted[i].Id))
                continue;

            var group = new List<Ticket>();
            for (var j = i; j < sorted.Count && group.Count < groupSize; j++)
            {
                if (!usedInPass.Contains(sorted[j].Id))
                    group.Add(sorted[j]);
            }

            if (group.Count == groupSize)
            {
                results.Add(group);
                foreach (var t in group)
                    usedInPass.Add(t.Id);
            }
        }

        return results;
    }

    private static TimeSpan TicketAge(Ticket ticket, DateTime now)
    {
        if (ticket.CreateTime == null)
            return TimeSpan.Zero;

        var created = ticket.CreateTime.ToDateTime();
        var age = now - created;
        return age > TimeSpan.Zero ? age : TimeSpan.Zero;
    }
}
