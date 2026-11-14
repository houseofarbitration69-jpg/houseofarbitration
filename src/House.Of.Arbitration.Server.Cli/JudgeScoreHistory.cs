using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.Server.Cli;

/// <summary>
/// Enregistrement d'une action ou d'une note attribuée par un juge.
/// </summary>
public class JudgeScoreEntry
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string MatchTimeDisplay { get; set; } = string.Empty;
    public int MatchId { get; set; }
    public string MatchCategory { get; set; } = string.Empty;
    public string MatchCompetitors { get; set; } = string.Empty;
    public string JudgeName { get; set; } = string.Empty;
    public string? JudgeGroup { get; set; }
    public int JudgeNumber { get; set; }
    public CompetitionType CompetitionType { get; set; }
    public Constants.Side? Side { get; set; }
    public double DeltaPoints { get; set; }
    public double ResultingScore { get; set; }
    public MvtTimeCodeModel? DeductionCode { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Synthèse de notation pour un match ou un juge.
/// </summary>
public class JudgeScoreSummary
{
    public int TotalEntries { get; set; }
    public int RedEntries { get; set; }
    public int BlueEntries { get; set; }
    public int DeductionEntries { get; set; }
    public IReadOnlyList<JudgeScoreEntry> Entries { get; set; } = Array.Empty<JudgeScoreEntry>();
}
