namespace SkullsLudo.MatchFunction.Strategies;

/// <summary>
/// Indexes registered <see cref="IMatchStrategy"/> implementations by their
/// <see cref="IMatchStrategy.Name"/>. Queues choose a strategy by reference
/// via <c>QueueConfiguration.Strategy</c>; multiple queues can share one.
/// </summary>
public sealed class MatchStrategyResolver
{
    private readonly Dictionary<string, IMatchStrategy> _strategies;

    public MatchStrategyResolver(IEnumerable<IMatchStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IMatchStrategy? Resolve(string strategyName)
        => string.IsNullOrEmpty(strategyName) ? null : _strategies.GetValueOrDefault(strategyName);

    public IReadOnlyCollection<string> RegisteredStrategies => _strategies.Keys;
}
