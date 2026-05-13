namespace SkullsLudo.Shared.Constants;

public static class WellKnown
{
    /// <summary>
    /// Stable identifiers for <c>IMatchStrategy</c> implementations. Queues reference
    /// these via <c>QueueConfiguration.Strategy</c>, so one strategy can serve many
    /// queues (e.g. <c>DegradingMmr</c> powers quickplay, classic, ranked, and casual).
    /// Queue names and tags are config data, not constants &mdash; see
    /// <c>DefaultQueues</c> and <c>Matchmaker:Queues</c> in <c>appsettings.json</c>.
    /// </summary>
    public static class Strategies
    {
        public const string Solo = "solo";
        public const string DegradingMmr = "degrading-mmr";
    }

    public static class SearchFields
    {
        public const string Mmr = "attribute.mmr";
        public const string PlayerId = "attribute.player_id";
    }

    public static class Extensions
    {
        public const string ScoreKey = "score";
        public const string PlayerCountKey = "player_count";
        public const string QueueKey = "queue";
        public const string TimeoutKey = "timeout";
    }

    public static class Ports
    {
        public const int MatchFunction = 50502;
        public const int Evaluator = 50508;
        public const int OpenMatchFrontend = 50504;
        public const int OpenMatchBackend = 50505;
        public const int OpenMatchQuery = 50503;
    }
}
