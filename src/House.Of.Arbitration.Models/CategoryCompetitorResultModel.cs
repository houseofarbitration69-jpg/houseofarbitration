using CommunityToolkit.Mvvm.ComponentModel;

namespace House.Of.Arbitration.Models;

public partial class CategoryCompetitorResultModel : ObservableObject
{
    public int Position { get; set; }
    public string RankDisplay { get; set; } = string.Empty;
    public string MedalIcon { get; set; } = string.Empty;
    public string MedalColorHex { get; set; } = "#808080";
    public bool IsPodium => Position >= 1 && Position <= 3;

    public int? CompetitorId { get; set; }
    public string CompetitorName { get; set; } = string.Empty;
    public string Club { get; set; } = string.Empty;
    public string CountryIsoCode { get; set; } = string.Empty;

    public double? NumericScore { get; set; }
    public string ScoreDisplay { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusColorHex { get; set; } = "#808080";

    public int MatchesWon { get; set; }
    public int MatchesLost { get; set; }
}
