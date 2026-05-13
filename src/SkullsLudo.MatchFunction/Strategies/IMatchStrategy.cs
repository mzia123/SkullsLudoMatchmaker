using OpenMatch;
using SkullsLudo.Shared.Configuration;

namespace SkullsLudo.MatchFunction.Strategies;

/// <summary>
/// A matching algorithm. Strategies are stateless and shared across all queues
/// that reference them via <see cref="QueueConfiguration.Strategy"/>. Per-queue
/// behaviour (player counts, MMR rules, timeouts, etc.) comes from the
/// <paramref name="queueConfig"/> passed to <see cref="CreateMatches"/>.
/// </summary>
public interface IMatchStrategy
{
    /// <summary>Stable identifier referenced by <see cref="QueueConfiguration.Strategy"/>.</summary>
    string Name { get; }

    IReadOnlyList<Match> CreateMatches(
        MatchProfile profile,
        IDictionary<string, IList<Ticket>> poolTickets,
        QueueConfiguration queueConfig);
}
