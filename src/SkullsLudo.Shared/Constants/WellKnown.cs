namespace SkullsLudo.Shared.Constants;

public static class WellKnown
{
    public static class Queues
    {
        public const string Practice = "practice";
        public const string Quickplay = "quickplay";
    }

    public static class Tags
    {
        public const string PracticeQueue = "queue.practice";
        public const string QuickplayQueue = "queue.quickplay";
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
