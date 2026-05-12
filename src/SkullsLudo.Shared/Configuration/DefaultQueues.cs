namespace SkullsLudo.Shared.Configuration;

public static class DefaultQueues
{
    public static QueueConfiguration Practice => new()
    {
        Name = "practice",
        Tag = "queue.practice",
        MaxPlayers = 2,
        MinPlayers = 2,
        Timeout = TimeSpan.FromMinutes(2),
        DegradationSteps = []
    };

    public static QueueConfiguration Quickplay => new()
    {
        Name = "quickplay",
        Tag = "queue.quickplay",
        MaxPlayers = 4,
        MinPlayers = 2,
        Timeout = TimeSpan.FromMinutes(2),
        DegradationSteps =
        [
            new DegradationStep { After = TimeSpan.FromSeconds(5), PlayerCount = 3 },
            new DegradationStep { After = TimeSpan.FromSeconds(10), PlayerCount = 2 }
        ]
    };

    public static Dictionary<string, QueueConfiguration> All => new()
    {
        ["practice"] = Practice,
        ["quickplay"] = Quickplay
    };
}
