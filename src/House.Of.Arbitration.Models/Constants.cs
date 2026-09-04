namespace House.Of.Arbitration.Models;

public static class Constants
{
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
    }
}
