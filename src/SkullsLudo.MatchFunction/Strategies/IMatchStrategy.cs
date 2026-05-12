using OpenMatch;

namespace SkullsLudo.MatchFunction.Strategies;

public interface IMatchStrategy
{
    string QueueName { get; }
    IReadOnlyList<Match> CreateMatches(MatchProfile profile, IDictionary<string, IList<Ticket>> poolTickets);
}
