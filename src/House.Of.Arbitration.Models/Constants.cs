namespace House.Of.Arbitration.Models;

public static class Constants
{
    public static class QueryParameter
    {
        public static string TITLE = "Title";
        public static string MESSAGE = "Message";
        public static string ACCEPT = "Accept";
        public static string CANCEL = "Cancel";
    }

    /// <summary>
    /// Local database constants (SQLite)
    /// </summary>
    public static class LocalDatabase
    {
        public static string DATABASE_NAME = "house_of_arbitration.db3";
    }

    public static class Message
    {
        public static string TIMER_START = "TIMER_START";
        public static string TIMER_PAUSE = "TIMER_PAUSE";
        public static string TIMER_STOP = "TIMER_STOP";
        public static string TIMER_SET = "TIMER_SET:";

        public static string COMPETITION_DATA = "COMPETITION_DATA:";
        public static string GET_COMPETITION = "GET_COMPETITION";
        public static string MATCH_INFO = "MATCH_INFO:";
        public static string JUDGE_POSITION = "JUDGE_POSITION:";
        public static string JUDGE_DISCONNECT = "JUDGE_DISCONNECT";
        public static string JUDGE_SCORE = "JUDGE_SCORE";
        public static string JUDGE_STATE = "JUDGE_STATE:";
        public static string JUDGE_SYNC_STATE = "JUDGE_SYNC_STATE:";
        public static string JUDGE_SYNC_ACK = "JUDGE_SYNC_ACK";
        public static string PING = "PING";
        public static string PONG = "PONG";
        public static string SERVER_STOPPED = "SERVER_STOPPED";
    }

    public static class Navigation
    {
        public static class Parameter
        {
            public const string COMPETITION_ID = "COMPETITION_ID";
        }

        public static class Page
        {
            public const string COMPETITION_WIZARD = "COMPETITION_WIZARD";
            public const string DRAWS_MANAGEMENT = "DRAWS_MANAGEMENT";
            public const string SERVER_TAOLU = "SERVER_TAOLU";
            public const string SERVER_SANDA = "SERVER_SANDA";
            public const string HOME = "HOME";
            public const string MASTER = "MASTER";
            public const string JUDGE = "JUDGE";
            public const string COMPETITIONS = "COMPETITIONS";
            public const string COMPETITORS = "COMPETITORS";
            public const string DRAWS = "DRAWS";
            public const string REFEREE_DATA = "REFEREE_DATA";
            public const string TAOLU_CODES = "TAOLU_CODES";
        }
    }
}