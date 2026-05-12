using Google.Protobuf.WellKnownTypes;
using OpenMatch;
using SkullsLudo.Shared.Constants;

namespace SkullsLudo.MatchFunction.Strategies;

public sealed class PracticeMatchStrategy : IMatchStrategy
{
    public string QueueName => WellKnown.Queues.Practice;

    public IReadOnlyList<Match> CreateMatches(
        MatchProfile profile,
        IDictionary<string, IList<Ticket>> poolTickets)
    {
        var allTickets = poolTickets.Values.SelectMany(t => t).ToList();
        var matches = new List<Match>();

        for (var i = 0; i + 1 < allTickets.Count; i += 2)
        {
            var group = new List<Ticket> { allTickets[i], allTickets[i + 1] };
            var score = MatchScoring.Calculate(group);

            var match = new Match
            {
                MatchId = $"practice-{Guid.NewGuid():N}",
                MatchProfile = profile.Name,
                MatchFunction = nameof(PracticeMatchStrategy)
            };
            match.Tickets.AddRange(group);
            match.Extensions[WellKnown.Extensions.ScoreKey] = Any.Pack(new DoubleValue { Value = score });
            match.Extensions[WellKnown.Extensions.PlayerCountKey] = Any.Pack(new Int32Value { Value = 2 });
            match.Extensions[WellKnown.Extensions.QueueKey] = Any.Pack(new StringValue { Value = QueueName });

            matches.Add(match);
        }

        return matches;
    }
}
