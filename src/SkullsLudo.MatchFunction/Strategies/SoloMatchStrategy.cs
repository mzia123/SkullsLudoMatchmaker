using Google.Protobuf.WellKnownTypes;
using OpenMatch;
using SkullsLudo.Shared.Configuration;
using SkullsLudo.Shared.Constants;

namespace SkullsLudo.MatchFunction.Strategies;

/// <summary>
/// One ticket per match. Useful for any single-player or training queue
/// (practice, tutorial, daily challenge, ...). Queue-agnostic: behaviour is
/// determined entirely by the <see cref="QueueConfiguration"/> passed in.
/// </summary>
public sealed class SoloMatchStrategy : IMatchStrategy
{
    public string Name => WellKnown.Strategies.Solo;

    public IReadOnlyList<Match> CreateMatches(
        MatchProfile profile,
        IDictionary<string, IList<Ticket>> poolTickets,
        QueueConfiguration queueConfig)
    {
        var allTickets = poolTickets.Values.SelectMany(t => t).ToList();
        var matches = new List<Match>(allTickets.Count);

        foreach (var ticket in allTickets)
        {
            var match = new Match
            {
                MatchId = $"{queueConfig.Name}-{Guid.NewGuid():N}",
                MatchProfile = profile.Name,
                MatchFunction = Name,
                Tickets = { ticket }
            };
            match.Extensions[WellKnown.Extensions.ScoreKey] = Any.Pack(new DoubleValue { Value = 1.0 });
            match.Extensions[WellKnown.Extensions.PlayerCountKey] = Any.Pack(new Int32Value { Value = 1 });
            match.Extensions[WellKnown.Extensions.QueueKey] = Any.Pack(new StringValue { Value = queueConfig.Name });

            matches.Add(match);
        }

        return matches;
    }
}
