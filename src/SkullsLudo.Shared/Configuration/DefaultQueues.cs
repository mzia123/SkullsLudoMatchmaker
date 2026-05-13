using SkullsLudo.Shared.Constants;

namespace SkullsLudo.Shared.Configuration;

/// <summary>
/// Canonical default queue catalogue. Used as a fallback when a service does not
/// supply a <c>Matchmaker:Queues</c> section in configuration. Keep this in sync
/// with <c>src/SkullsLudo.Director/appsettings.json</c> (or replace both with a
/// shared ConfigMap mount in production).
/// </summary>
public static class DefaultQueues
{
    public static Dictionary<string, QueueConfiguration> All => new()
    {
        ["practice-team"] = new QueueConfiguration
        {
            Name = "practice-team",
            Tag = "queue.practice-team",
            Strategy = WellKnown.Strategies.Solo,
            MaxPlayers = 4,
            MinPlayers = 1,
            Timeout = TimeSpan.FromSeconds(30),
            DegradationSteps = []
        },

        ["practice-nonteam"] = new QueueConfiguration
        {
            Name = "practice-nonteam",
            Tag = "queue.practice-nonteam",
            Strategy = WellKnown.Strategies.Solo,
            MaxPlayers = 4,
            MinPlayers = 1,
            Timeout = TimeSpan.FromSeconds(30),
            DegradationSteps = []
        },

        ["quickplay-team"] = new QueueConfiguration
        {
            Name = "quickplay-team",
            Tag = "queue.quickplay-team",
            Strategy = WellKnown.Strategies.DegradingMmr,
            MaxPlayers = 4,
            MinPlayers = 2,
            Timeout = TimeSpan.FromMinutes(1),
            DegradationSteps =
            [
                new DegradationStep { After = TimeSpan.FromSeconds(15), PlayerCount = 2 }
            ]
        },

        ["quickplay-nonteam"] = new QueueConfiguration
        {
            Name = "quickplay-nonteam",
            Tag = "queue.quickplay-nonteam",
            Strategy = WellKnown.Strategies.DegradingMmr,
            MaxPlayers = 4,
            MinPlayers = 2,
            Timeout = TimeSpan.FromMinutes(1),
            DegradationSteps =
            [
                new DegradationStep { After = TimeSpan.FromSeconds(5), PlayerCount = 3 },
                new DegradationStep { After = TimeSpan.FromSeconds(15), PlayerCount = 2 }
            ]
        },

        ["classic-team"] = new QueueConfiguration
        {
            Name = "classic-team",
            Tag = "queue.classic-team",
            Strategy = WellKnown.Strategies.DegradingMmr,
            MaxPlayers = 4,
            MinPlayers = 2,
            Timeout = TimeSpan.FromMinutes(2),
            DegradationSteps =
            [
                new DegradationStep { After = TimeSpan.FromSeconds(30), PlayerCount = 2 }
            ]
        },

        ["classic-nonteam"] = new QueueConfiguration
        {
            Name = "classic-nonteam",
            Tag = "queue.classic-nonteam",
            Strategy = WellKnown.Strategies.DegradingMmr,
            MaxPlayers = 4,
            MinPlayers = 2,
            Timeout = TimeSpan.FromMinutes(2),
            DegradationSteps =
            [
                new DegradationStep { After = TimeSpan.FromSeconds(20), PlayerCount = 3 },
                new DegradationStep { After = TimeSpan.FromSeconds(45), PlayerCount = 2 }
            ]
        },

        ["ranked"] = new QueueConfiguration
        {
            Name = "ranked",
            Tag = "queue.ranked",
            Strategy = WellKnown.Strategies.DegradingMmr,
            MaxPlayers = 4,
            MinPlayers = 4,
            Timeout = TimeSpan.FromMinutes(5),
            DegradationSteps = []
        },

        ["casual"] = new QueueConfiguration
        {
            Name = "casual",
            Tag = "queue.casual",
            Strategy = WellKnown.Strategies.DegradingMmr,
            MaxPlayers = 4,
            MinPlayers = 2,
            Timeout = TimeSpan.FromMinutes(2),
            DegradationSteps =
            [
                new DegradationStep { After = TimeSpan.FromSeconds(15), PlayerCount = 3 },
                new DegradationStep { After = TimeSpan.FromSeconds(30), PlayerCount = 2 }
            ]
        }
    };
}
