namespace SkullsLudo.Shared.Configuration;

public static class QueueConfigLoader
{
    /// <summary>
    /// Path of the queue catalogue file mounted from the <c>skulls-ludo-queues</c>
    /// Kubernetes ConfigMap. Each matchmaker service registers this as an optional
    /// JSON configuration source, so a single ConfigMap edit + rollout propagates
    /// queue changes without an image rebuild. If the file is absent the services
    /// fall back to <see cref="DefaultQueues.All"/> via <see cref="EnsureQueues"/>.
    /// </summary>
    public const string QueuesFilePath = "/config/queues.json";

    /// <summary>
    /// Populates <see cref="MatchmakerSettings.Queues"/> from
    /// <see cref="DefaultQueues.All"/> when no queues were loaded from configuration
    /// (e.g. the ConfigMap is not deployed). Returns the same instance for fluent use.
    /// </summary>
    public static MatchmakerSettings EnsureQueues(this MatchmakerSettings settings)
    {
        if (settings.Queues.Count == 0)
        {
            foreach (var (key, value) in DefaultQueues.All)
                settings.Queues[key] = value;
        }
        return settings;
    }
}
