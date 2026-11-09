#region Imports
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
#endregion

namespace House.Of.Arbitration.ViewModels.Competition;

public partial class ResultsViewModel : BaseViewModel, IQueryAttributable
{
    #region Services
    private readonly IRepository<CompetitionModel> _competitionRepository;
    private readonly IRepository<CategoryModel> _categoryRepository;
    private readonly IRepository<DrawModel> _drawRepository;
    private readonly IRepository<RefereeDataModel> _refereeDataRepository;
    #endregion

    #region Fields
    private int _competitionId;
    private CompetitionModel? _competition;
    private ObservableCollection<CategoryModel> _categories = new();
    private CategoryModel? _selectedCategory;
    private ObservableCollection<CategoryCompetitorResultModel> _results = new();
    private CategoryCompetitorResultModel? _firstPlace;
    private CategoryCompetitorResultModel? _secondPlace;
    private CategoryCompetitorResultModel? _thirdPlace;
    private bool _hasPodium;
    private bool _hasResults;
    private string _categoryStatusText = string.Empty;
    #endregion

    #region Properties
    public int CompetitionId
    {
        get => _competitionId;
        set => SetProperty(ref _competitionId, value);
    }

    public CompetitionModel? Competition
    {
        get => _competition;
        set => SetProperty(ref _competition, value);
    }

    public ObservableCollection<CategoryModel> Categories
    {
        get => _categories;
        set => SetProperty(ref _categories, value);
    }

    public CategoryModel? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                _ = LoadResultsForSelectedCategoryAsync();
            }
        }
    }

    public ObservableCollection<CategoryCompetitorResultModel> Results
    {
        get => _results;
        set => SetProperty(ref _results, value);
    }

    public CategoryCompetitorResultModel? FirstPlace
    {
        get => _firstPlace;
        set => SetProperty(ref _firstPlace, value);
    }

    public CategoryCompetitorResultModel? SecondPlace
    {
        get => _secondPlace;
        set => SetProperty(ref _secondPlace, value);
    }

    public CategoryCompetitorResultModel? ThirdPlace
    {
        get => _thirdPlace;
        set => SetProperty(ref _thirdPlace, value);
    }

    public bool HasPodium
    {
        get => _hasPodium;
        set => SetProperty(ref _hasPodium, value);
    }

    public bool HasResults
    {
        get => _hasResults;
        set => SetProperty(ref _hasResults, value);
    }

    public string CategoryStatusText
    {
        get => _categoryStatusText;
        set => SetProperty(ref _categoryStatusText, value);
    }
    #endregion

    #region Constructor
    public ResultsViewModel(
        ILogger<ResultsViewModel> logger,
        ResourceProvider resourceProvider,
        IPopupService popupService,
        IRepository<CompetitionModel> competitionRepository,
        IRepository<CategoryModel> categoryRepository,
        IRepository<DrawModel> drawRepository,
        IRepository<RefereeDataModel> refereeDataRepository)
        : base(logger, resourceProvider, popupService)
    {
        _competitionRepository = competitionRepository;
        _categoryRepository = categoryRepository;
        _drawRepository = drawRepository;
        _refereeDataRepository = refereeDataRepository;
    }
    #endregion

    #region IQueryAttributable
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(Constants.Navigation.Parameter.COMPETITION_ID, out var compIdObj) ||
            query.TryGetValue("CompetitionId", out compIdObj))
        {
            if (compIdObj is int id)
            {
                CompetitionId = id;
            }
            else if (int.TryParse(compIdObj?.ToString(), out int parsedId))
            {
                CompetitionId = parsedId;
            }
        }
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task Back()
    {
        await Shell.Current.GoToAsync("..");
    }
    #endregion

    #region Lifecycle
    public override async Task OnAppearing()
    {
        IsBusy = true;
        try
        {
            if (CompetitionId > 0)
            {
                Competition = await _competitionRepository.GetByIdAsync(CompetitionId);
            }

            var allCategories = await _categoryRepository.GetAllAsync(
                "AgeRange",
                "Competitors.Competitor.Country");

            var compCats = allCategories?.Where(c => c.CompetitionId == CompetitionId).ToList() ?? new List<CategoryModel>();
            Categories = new ObservableCollection<CategoryModel>(compCats);

            if (SelectedCategory == null || !Categories.Any(c => c.Id == SelectedCategory.Id))
            {
                SelectedCategory = Categories.FirstOrDefault();
            }
            else
            {
                await LoadResultsForSelectedCategoryAsync();
            }

            await base.OnAppearing();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur chargement des résultats");
        }
        finally
        {
            IsBusy = false;
        }
    }
    #endregion

    #region Logic
    public async Task LoadResultsForSelectedCategoryAsync()
    {
        if (SelectedCategory == null)
        {
            Results = new ObservableCollection<CategoryCompetitorResultModel>();
            HasResults = false;
            HasPodium = false;
            FirstPlace = null;
            SecondPlace = null;
            ThirdPlace = null;
            CategoryStatusText = "Aucune catégorie sélectionnée.";
            return;
        }

        try
        {
            var category = SelectedCategory;
            var allDraws = await _drawRepository.GetAllAsync(
                "DrawKnockouts.Competitor1.Country",
                "DrawKnockouts.Competitor2.Country",
                "DrawKnockouts.Winner",
                "DrawKnockouts.Looser",
                "DrawPools.Competitor1.Country",
                "DrawPools.Competitor2.Country",
                "DrawPools.Winner",
                "DrawPools.Looser",
                "DrawOrders.Competitor.Country");

            var draw = allDraws?.FirstOrDefault(d => d.CategoryId == category.Id);
            var computedResults = new List<CategoryCompetitorResultModel>();

            bool isTaolu = category.RoundType == RoundType.Order ||
                           category.Type == CategoryType.Taolu ||
                           category.Type == CategoryType.TaoluModerneNord ||
                           category.Type == CategoryType.TaoluModerneSud;

            if (isTaolu && draw?.DrawOrders != null && draw.DrawOrders.Count > 0)
            {
                // Taolu : classement par score décroissant
                computedResults = await ComputeTaoluResultsAsync(draw.DrawOrders);
            }
            else if (category.RoundType == RoundType.Knockouts && draw?.DrawKnockouts != null && draw.DrawKnockouts.Count > 0)
            {
                // Sanda Élimination directe (Knockout)
                computedResults = ComputeKnockoutResults(draw.DrawKnockouts, category);
            }
            else if (category.RoundType == RoundType.Pools && draw?.DrawPools != null && draw.DrawPools.Count > 0)
            {
                // Sanda Poules
                computedResults = ComputePoolResults(draw.DrawPools, category);
            }
            else
            {
                // Pas de tirage ou pas de matchs : lister les compétiteurs inscrits
                int pos = 1;
                foreach (var cc in category.Competitors ?? Enumerable.Empty<CompetitorCategoryModel>())
                {
                    if (cc.Competitor != null)
                    {
                        computedResults.Add(new CategoryCompetitorResultModel
                        {
                            Position = pos,
                            RankDisplay = $"{pos}",
                            CompetitorId = cc.Competitor.Id,
                            CompetitorName = cc.Competitor.Name,
                            Club = cc.Competitor.Club,
                            CountryIsoCode = cc.Competitor.CountryIsoCode,
                            ScoreDisplay = "En attente",
                            Status = "Non démarré",
                            StatusColorHex = "#808080"
                        });
                        pos++;
                    }
                }
            }

            Results = new ObservableCollection<CategoryCompetitorResultModel>(computedResults);
            HasResults = Results.Count > 0;

            FirstPlace = Results.FirstOrDefault(r => r.Position == 1 && r.Status != "Non démarré");
            SecondPlace = Results.FirstOrDefault(r => r.Position == 2 && r.Status != "Non démarré");
            ThirdPlace = Results.FirstOrDefault(r => r.Position == 3 && r.Status != "Non démarré");
            HasPodium = FirstPlace != null;

            int finishedCount = Results.Count(r => r.Status == "Terminé" || r.Status == "Médaille d'or" || r.Status == "Vainqueur");
            CategoryStatusText = $"{Results.Count} compétiteur(s) • {finishedCount} terminé(s)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur calcul des résultats pour la catégorie {CategoryId}", SelectedCategory.Id);
        }
    }

    private async Task<List<CategoryCompetitorResultModel>> ComputeTaoluResultsAsync(List<DrawOrderModel> orders)
    {
        var list = new List<CategoryCompetitorResultModel>();
        var allRefDatas = await _refereeDataRepository.GetAllAsync();

        foreach (var order in orders)
        {
            if (order.Competitor == null) continue;

            double? numericScore = null;
            if (!string.IsNullOrEmpty(order.RoundResultsJson) &&
                double.TryParse(order.RoundResultsJson, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedScore))
            {
                numericScore = parsedScore;
            }
            else if (order.IsFinished)
            {
                // Calcul basé sur les notes des arbitres si disponibles
                var datasForOrder = allRefDatas?.Where(r => r.DrawOrderId == order.Id && r.CompetitorId == order.CompetitorId).ToList();
                if (datasForOrder != null && datasForOrder.Count > 0)
                {
                    double deductions = datasForOrder.Where(d => !d.IsCorrection).Sum(d => Math.Abs(d.Score));
                    numericScore = Math.Max(0.0, Math.Round(10.0 - deductions, 2));
                }
            }

            list.Add(new CategoryCompetitorResultModel
            {
                CompetitorId = order.Competitor.Id,
                CompetitorName = order.Competitor.Name,
                Club = order.Competitor.Club,
                CountryIsoCode = order.Competitor.CountryIsoCode,
                NumericScore = numericScore,
                ScoreDisplay = numericScore.HasValue ? $"{numericScore.Value:F2} pts" : "—",
                Status = order.IsFinished ? "Terminé" : "En attente",
                StatusColorHex = order.IsFinished ? "#4CAF50" : "#808080"
            });
        }

        // Tri : les scores terminés les plus élevés en premier, puis les non-passés
        var sorted = list
            .OrderByDescending(r => r.NumericScore.HasValue)
            .ThenByDescending(r => r.NumericScore ?? 0)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            int rank = i + 1;
            sorted[i].Position = rank;
            sorted[i].RankDisplay = rank == 1 ? "1er" : $"{rank}e";
            sorted[i].MedalIcon = rank switch
            {
                1 => "🥇",
                2 => "🥈",
                3 => "🥉",
                _ => string.Empty
            };
            sorted[i].MedalColorHex = rank switch
            {
                1 => "#FFD700",
                2 => "#C0C0C0",
                3 => "#CD7F32",
                _ => "#808080"
            };
        }

        return sorted;
    }

    private List<CategoryCompetitorResultModel> ComputeKnockoutResults(List<DrawKnockoutModel> matches, CategoryModel category)
    {
        var list = new List<CategoryCompetitorResultModel>();
        var competitorsDict = new Dictionary<int, CompetitorModel>();

        foreach (var m in matches)
        {
            if (m.Competitor1 != null && !competitorsDict.ContainsKey(m.Competitor1.Id)) competitorsDict[m.Competitor1.Id] = m.Competitor1;
            if (m.Competitor2 != null && !competitorsDict.ContainsKey(m.Competitor2.Id)) competitorsDict[m.Competitor2.Id] = m.Competitor2;
        }

        var finalMatch = matches.OrderBy(m => m.Order).LastOrDefault();
        int? championId = finalMatch?.WinnerId;
        int? viceChampionId = finalMatch?.LooserId;

        // Demi-finales (matches juste avant la finale)
        var semiFinalLooserIds = new HashSet<int>();
        if (matches.Count >= 3)
        {
            int total = matches.Count;
            // Dans un arbre binaire, le match de finale est à l'index total-1, et les demis sont total-2 et total-3
            var semi1 = matches.ElementAtOrDefault(total - 2);
            var semi2 = matches.ElementAtOrDefault(total - 3);

            if (semi1?.LooserId.HasValue == true && semi1.LooserId != viceChampionId)
                semiFinalLooserIds.Add(semi1.LooserId.Value);
            if (semi2?.LooserId.HasValue == true && semi2.LooserId != viceChampionId)
                semiFinalLooserIds.Add(semi2.LooserId.Value);
        }

        foreach (var kvp in competitorsDict)
        {
            var comp = kvp.Value;
            int wins = matches.Count(m => m.WinnerId == comp.Id && m.IsFinished);
            int losses = matches.Count(m => m.LooserId == comp.Id && m.IsFinished);

            int rank = 99;
            string status = "En lice";
            string statusColor = "#2196F3";

            if (comp.Id == championId)
            {
                rank = 1;
                status = "Vainqueur 🥇";
                statusColor = "#FFD700";
            }
            else if (comp.Id == viceChampionId)
            {
                rank = 2;
                status = "Finaliste 🥈";
                statusColor = "#C0C0C0";
            }
            else if (semiFinalLooserIds.Contains(comp.Id))
            {
                rank = 3;
                status = "1/2 Finale 🥉";
                statusColor = "#CD7F32";
            }
            else if (losses > 0)
            {
                rank = 4 + (matches.Count - wins);
                status = "Éliminé";
                statusColor = "#808080";
            }

            list.Add(new CategoryCompetitorResultModel
            {
                Position = rank,
                CompetitorId = comp.Id,
                CompetitorName = comp.Name,
                Club = comp.Club,
                CountryIsoCode = comp.CountryIsoCode,
                MatchesWon = wins,
                MatchesLost = losses,
                ScoreDisplay = $"{wins}V - {losses}D",
                Status = status,
                StatusColorHex = statusColor
            });
        }

        var sorted = list.OrderBy(r => r.Position).ThenByDescending(r => r.MatchesWon).ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            int rank = i + 1;
            sorted[i].Position = rank;
            sorted[i].RankDisplay = rank == 1 ? "1er" : $"{rank}e";
            sorted[i].MedalIcon = rank switch
            {
                1 => "🥇",
                2 => "🥈",
                3 => "🥉",
                _ => string.Empty
            };
            sorted[i].MedalColorHex = rank switch
            {
                1 => "#FFD700",
                2 => "#C0C0C0",
                3 => "#CD7F32",
                _ => "#808080"
            };
        }

        return sorted;
    }

    private List<CategoryCompetitorResultModel> ComputePoolResults(List<DrawPoolsModel> matches, CategoryModel category)
    {
        var list = new List<CategoryCompetitorResultModel>();
        var competitorsDict = new Dictionary<int, CompetitorModel>();

        foreach (var m in matches)
        {
            if (m.Competitor1 != null && !competitorsDict.ContainsKey(m.Competitor1.Id)) competitorsDict[m.Competitor1.Id] = m.Competitor1;
            if (m.Competitor2 != null && !competitorsDict.ContainsKey(m.Competitor2.Id)) competitorsDict[m.Competitor2.Id] = m.Competitor2;
        }

        foreach (var kvp in competitorsDict)
        {
            var comp = kvp.Value;
            int wins = matches.Count(m => m.WinnerId == comp.Id && m.IsFinished);
            int losses = matches.Count(m => m.LooserId == comp.Id && m.IsFinished);

            list.Add(new CategoryCompetitorResultModel
            {
                CompetitorId = comp.Id,
                CompetitorName = comp.Name,
                Club = comp.Club,
                CountryIsoCode = comp.CountryIsoCode,
                MatchesWon = wins,
                MatchesLost = losses,
                ScoreDisplay = $"{wins}V - {losses}D",
                Status = $"{wins} victoire(s)",
                StatusColorHex = wins > 0 ? "#4CAF50" : "#808080"
            });
        }

        var sorted = list.OrderByDescending(r => r.MatchesWon).ThenBy(r => r.MatchesLost).ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            int rank = i + 1;
            sorted[i].Position = rank;
            sorted[i].RankDisplay = rank == 1 ? "1er" : $"{rank}e";
            sorted[i].MedalIcon = rank switch
            {
                1 => "🥇",
                2 => "🥈",
                3 => "🥉",
                _ => string.Empty
            };
            sorted[i].MedalColorHex = rank switch
            {
                1 => "#FFD700",
                2 => "#C0C0C0",
                3 => "#CD7F32",
                _ => "#808080"
            };
        }

        return sorted;
    }
    #endregion
}
