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
        var matches = new List<Match>(allTickets.Count);

        foreach (var ticket in allTickets)
        {
            var match = new Match
            {
                MatchId = $"practice-{Guid.NewGuid():N}",
                MatchProfile = profile.Name,
                MatchFunction = nameof(PracticeMatchStrategy),
                Tickets = { ticket }
            };
            match.Extensions[WellKnown.Extensions.ScoreKey] = Any.Pack(new DoubleValue { Value = 1.0 });
            match.Extensions[WellKnown.Extensions.PlayerCountKey] = Any.Pack(new Int32Value { Value = 1 });
            match.Extensions[WellKnown.Extensions.QueueKey] = Any.Pack(new StringValue { Value = QueueName });

            matches.Add(match);
        }

        return matches;
    }
}
