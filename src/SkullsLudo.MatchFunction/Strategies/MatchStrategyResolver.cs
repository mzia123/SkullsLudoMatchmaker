namespace SkullsLudo.MatchFunction.Strategies;

public sealed class MatchStrategyResolver
{
    private readonly Dictionary<string, IMatchStrategy> _strategies;

    public MatchStrategyResolver(IEnumerable<IMatchStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(s => s.QueueName, StringComparer.OrdinalIgnoreCase);
    }

    public IMatchStrategy? Resolve(string profileName)
    {
        return _strategies.GetValueOrDefault(profileName);
    }

    public IReadOnlyCollection<string> RegisteredQueues => _strategies.Keys;
}
