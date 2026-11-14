using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.Server.Cli;

/// <summary>
/// Synthèse globale de l'état d'avancement d'une catégorie.
/// </summary>
public class CategorySummary
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public RoundType RoundType { get; set; } = RoundType.Knockouts;
    public bool HasBronzeMatch { get; set; }
    public int TotalMatches { get; set; }
    public int CompletedMatches { get; set; }
    public int RemainingMatches => Math.Max(0, TotalMatches - CompletedMatches);
    public double ProgressPercentage => TotalMatches > 0 ? (CompletedMatches * 100.0 / TotalMatches) : 0.0;
    public bool IsFinished => TotalMatches > 0 && CompletedMatches >= TotalMatches;
    public bool IsInProgress => CompletedMatches > 0 && CompletedMatches < TotalMatches;
    public string StatusText => IsFinished ? "Terminée" : (IsInProgress ? "En cours" : "À venir");
    public string? LeaderOrWinner { get; set; }
    public IReadOnlyList<MatchItem> Matches { get; set; } = Array.Empty<MatchItem>();
}

/// <summary>
/// Ligne de classement / bilan d'un compétiteur au sein d'une catégorie.
/// </summary>
public class CategoryStandingItem
{
    public int Rank { get; set; }
    public string CompetitorName { get; set; } = string.Empty;
    public int MatchesPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public int PointsFor { get; set; }
    public int PointsAgainst { get; set; }
    public int PointDifference => PointsFor - PointsAgainst;
    public double Score { get; set; }
    public bool HasFinished { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Rapport complet d'historique et classement d'une catégorie.
/// </summary>
public class CategoryHistoryReport
{
    public CategorySummary Summary { get; set; } = new();
    public IReadOnlyList<MatchItem> Matches { get; set; } = Array.Empty<MatchItem>();
    public IReadOnlyList<CategoryStandingItem> Standings { get; set; } = Array.Empty<CategoryStandingItem>();
    public string? CategoryWinner { get; set; }
    public string? DecisionReason { get; set; }
}
