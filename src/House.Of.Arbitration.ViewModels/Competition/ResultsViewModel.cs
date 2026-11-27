#region Imports
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Models.Helpers;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
#endregion

namespace House.Of.Arbitration.ViewModels.Competition;

[QueryProperty(nameof(CompetitionId), Constants.Navigation.Parameter.COMPETITION_ID)]
[QueryProperty(nameof(CurrentCategoryId), Constants.Navigation.Parameter.CATEGORY_ID)]
public partial class ResultsViewModel : BaseViewModel, IQueryAttributable
{
    #region Services
    private readonly IRepository<CompetitionModel> _competitionRepository;
    private readonly IRepository<CategoryModel> _categoryRepository;
    private readonly IRepository<DrawModel> _drawRepository;
    private readonly IRepository<RefereeDataModel> _refereeDataRepository;
    private readonly IExcelImportExportService _excelService;
    private readonly IFileDownloadService _fileDownloadService;
    #endregion

    #region Attributs
    private int _competitionId;
    private int _currentCategoryId;

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

    public int CurrentCategoryId
    {
        get => _currentCategoryId;
        set => SetProperty(ref _currentCategoryId, value);
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
        IRepository<RefereeDataModel> refereeDataRepository,
        IExcelImportExportService excelService,
        IFileDownloadService fileDownloadService)
        : base(logger, resourceProvider, popupService)
    {
        _competitionRepository = competitionRepository;
        _categoryRepository = categoryRepository;
        _drawRepository = drawRepository;
        _refereeDataRepository = refereeDataRepository;
        _excelService = excelService;
        _fileDownloadService = fileDownloadService;
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

        if (query.TryGetValue(Constants.Navigation.Parameter.CATEGORY_ID, out var catObj) &&
            int.TryParse(catObj?.ToString(), out int catId))
        {
            CurrentCategoryId = catId;
        }
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task ExportToExcel()
    {
        if (Categories == null || !Categories.Any())
        {
            await Shell.Current.DisplayAlertAsync("Export", "Aucune catégorie à exporter.", "OK");
            return;
        }

        IsBusy = true;
        try
        {
            var exportData = new List<CategoryResultExportData>();

            // Charger les résultats de toutes les catégories
            foreach (var category in Categories)
            {
                var rows = await LoadResultsForCategoryAsync(category);
                exportData.Add(new CategoryResultExportData
                {
                    CategoryName = category.Name ?? $"Catégorie {category.Id}",
                    CategoryType = GetCategoryTypeLabel(category),
                    Rows = rows.Select(r => new CompetitorResultExportRow
                    {
                        Position = r.Position,
                        Medal = r.MedalIcon ?? string.Empty,
                        CompetitorName = r.CompetitorName ?? string.Empty,
                        Club = r.Club ?? string.Empty,
                        Country = r.CountryIsoCode ?? string.Empty,
                        Score = r.ScoreDisplay ?? string.Empty,
                        Status = r.Status ?? string.Empty
                    }).ToList()
                });
            }

            string competitionName = Competition?.Name ?? "Compétition";
            DateTime? competitionDate = Competition?.Date;

            var stream = await _excelService.ExportResultsAsync(competitionName, competitionDate, exportData);

            string fileName = $"Resultats_{SanitizeFileName(competitionName)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            string? savedPath = await _fileDownloadService.SaveToDownloadsAsync(stream, fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

            if (savedPath != null)
            {
                await Shell.Current.DisplayAlert("Export réussi", $"Fichier sauvegardé :\n{savedPath}", "OK");
            }
            else
            {
                await Shell.Current.DisplayAlert("Export", "Le fichier n'a pas pu être sauvegardé.", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'export Excel des résultats");
            await Shell.Current.DisplayAlert("Erreur", "Une erreur est survenue lors de l'export Excel.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
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

            // Récupérer les notes individuelles de chaque juge
            var judgeScores = allRefDatas?
                .Where(r => r.DrawOrderId == order.Id && r.CompetitorId == order.CompetitorId && !r.IsCorrection)
                .Select(r => r.Score)
                .ToList() ?? new List<double>();

            var judgeScoresByJudge = allRefDatas?
                .Where(r => r.DrawOrderId == order.Id && r.CompetitorId == order.CompetitorId && !r.IsCorrection
                            && !string.IsNullOrWhiteSpace(r.Referee))
                .GroupBy(r => r.Referee)
                .ToDictionary(g => g.Key, g => g.First().Score)
                ?? new Dictionary<string, double>();

            list.Add(new CategoryCompetitorResultModel
            {
                CompetitorId = order.Competitor.Id,
                CompetitorName = order.Competitor.Name,
                Club = order.Competitor.Club,
                CountryIsoCode = order.Competitor.CountryIsoCode,
                NumericScore = numericScore,
                ScoreDisplay = numericScore.HasValue ? $"{numericScore.Value:F2} pts" : "—",
                Status = order.IsFinished ? "Terminé" : "En attente",
                StatusColorHex = order.IsFinished ? "#4CAF50" : "#808080",
                JudgeScores = judgeScores,
                JudgeScoresByJudge = judgeScoresByJudge
            });
        }

        // Tri : finis d'abord, puis départage sur les notes individuelles
        var finished = list.Where(r => r.NumericScore.HasValue).ToList();
        var pending = list.Where(r => !r.NumericScore.HasValue).ToList();

        var sortedFinished = finished
            .OrderByDescending(r => r.NumericScore ?? 0)
            .ThenByDescending(r => r.JudgeScores.Count > 0 ? r.JudgeScores.Max() : 0)
            .ThenByDescending(r => r.JudgeScores.Count > 0 ? r.JudgeScores.Min() : 0)
            .ToList();

        int rank = 1;
        for (int i = 0; i < sortedFinished.Count; i++)
        {
            if (i > 0)
            {
                var prev = sortedFinished[i - 1];
                var curr = sortedFinished[i];
                bool sameTotal = Math.Abs((prev.NumericScore ?? 0) - (curr.NumericScore ?? 0)) < 0.001;
                bool sameMax = prev.JudgeScores.Count == 0 || curr.JudgeScores.Count == 0
                    || Math.Abs(prev.JudgeScores.Max() - curr.JudgeScores.Max()) < 0.001;
                bool sameMin = prev.JudgeScores.Count == 0 || curr.JudgeScores.Count == 0
                    || Math.Abs(prev.JudgeScores.Min() - curr.JudgeScores.Min()) < 0.001;
                if (!(sameTotal && sameMax && sameMin))
                    rank = i + 1;
            }
            sortedFinished[i].Position = rank;
            sortedFinished[i].RankDisplay = rank == 1 ? "1er" : $"{rank}e";
            sortedFinished[i].MedalIcon = rank switch
            {
                1 => "🥇",
                2 => "🥈",
                3 => "🥉",
                _ => string.Empty
            };
            sortedFinished[i].MedalColorHex = rank switch
            {
                1 => "#FFD700",
                2 => "#C0C0C0",
                3 => "#CD7F32",
                _ => "#808080"
            };
        }

        // Les non-terminés viennent après
        int pendingStart = sortedFinished.Count > 0 ? sortedFinished.Max(x => x.Position) + 1 : 1;
        for (int i = 0; i < pending.Count; i++)
        {
            int pRank = pendingStart + i;
            pending[i].Position = pRank;
            pending[i].RankDisplay = $"{pRank}e";
            pending[i].MedalIcon = string.Empty;
            pending[i].MedalColorHex = "#808080";
        }

        return sortedFinished.Concat(pending).ToList();
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

        var finalMatch = matches.Where(m => !m.IsBronzeMatch).OrderBy(m => m.Order).LastOrDefault() ?? matches.OrderBy(m => m.Order).LastOrDefault();
        int? championId = finalMatch?.WinnerId;
        int? viceChampionId = finalMatch?.LooserId;

        var bronzeMatch = matches.FirstOrDefault(m => m.IsBronzeMatch);
        int? bronzeWinnerId = (bronzeMatch?.IsFinished == true) ? bronzeMatch.WinnerId : null;
        int? bronzeLooserId = (bronzeMatch?.IsFinished == true) ? bronzeMatch.LooserId : null;

        // Demi-finales (matches juste avant la finale)
        var semiFinalLooserIds = new HashSet<int>();
        var nonFinalMatches = matches.Where(m => !m.IsBronzeMatch && (finalMatch == null || m.Id != finalMatch.Id)).ToList();
        if (nonFinalMatches.Count >= 2)
        {
            var semi1 = nonFinalMatches[^1];
            var semi2 = nonFinalMatches[^2];

            if (semi1.LooserId.HasValue && semi1.LooserId != viceChampionId)
                semiFinalLooserIds.Add(semi1.LooserId.Value);
            if (semi2.LooserId.HasValue && semi2.LooserId != viceChampionId)
                semiFinalLooserIds.Add(semi2.LooserId.Value);
        }

        // Calcul des points cumulés par compétiteur (si notes arbitres ou différentiel)
        var pointsByComp = new Dictionary<int, double>();
        foreach (var kvp in competitorsDict)
        {
            double totalPts = 0;
            var compMatches = matches.Where(m => m.IsFinished && (m.Competitor1Id == kvp.Key || m.Competitor2Id == kvp.Key));
            foreach (var cm in compMatches)
            {
                if (cm.RefereeDatas != null && cm.RefereeDatas.Count > 0)
                {
                    totalPts += cm.RefereeDatas.Where(rd => rd.CompetitorId == kvp.Key).Sum(rd => (double)rd.Score);
                }
            }
            pointsByComp[kvp.Key] = totalPts;
        }

        int? bronzeByPointsId = null;
        int? fourthByPointsId = null;
        if (bronzeMatch == null && semiFinalLooserIds.Count > 0)
        {
            var sortedSemiLosers = semiFinalLooserIds
                .OrderByDescending(id => pointsByComp.GetValueOrDefault(id, 0))
                .ThenByDescending(id => matches.Count(m => m.WinnerId == id && m.IsFinished))
                .ToList();

            if (sortedSemiLosers.Count >= 1) bronzeByPointsId = sortedSemiLosers[0];
            if (sortedSemiLosers.Count >= 2) fourthByPointsId = sortedSemiLosers[1];
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
            else if (bronzeWinnerId.HasValue && comp.Id == bronzeWinnerId.Value)
            {
                rank = 3;
                status = "3e place (Petite finale) 🥉";
                statusColor = "#CD7F32";
            }
            else if (bronzeLooserId.HasValue && comp.Id == bronzeLooserId.Value)
            {
                rank = 4;
                status = "4e place (Petite finale)";
                statusColor = "#808080";
            }
            else if (bronzeMatch != null && (bronzeMatch.Competitor1Id == comp.Id || bronzeMatch.Competitor2Id == comp.Id))
            {
                rank = 3;
                status = "Qualifié Petite finale 🥉";
                statusColor = "#CD7F32";
            }
            else if (bronzeMatch == null && bronzeByPointsId.HasValue && comp.Id == bronzeByPointsId.Value)
            {
                rank = 3;
                status = "3e place (Points cumulés) 🥉";
                statusColor = "#CD7F32";
            }
            else if (bronzeMatch == null && fourthByPointsId.HasValue && comp.Id == fourthByPointsId.Value)
            {
                rank = 4;
                status = "4e place";
                statusColor = "#808080";
            }
            else if (semiFinalLooserIds.Contains(comp.Id))
            {
                rank = 5;
                status = "Demi-finale";
                statusColor = "#CD7F32";
            }
            else if (losses > 0)
            {
                var lostMatch = matches.FirstOrDefault(m => m.LooserId == comp.Id);
                if (lostMatch != null)
                {
                    status = $"Éliminé ({lostMatch.StageName})";
                }
                else
                {
                    status = "Éliminé";
                }
                rank = 6 + (matches.Count - wins);
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

    /// <summary>
    /// Charge les résultats d'une catégorie donnée (sans changer la sélection courante).
    /// </summary>
    private async Task<List<CategoryCompetitorResultModel>> LoadResultsForCategoryAsync(CategoryModel category)
    {
        try
        {
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
                computedResults = await ComputeTaoluResultsAsync(draw.DrawOrders);
            }
            else if (category.RoundType == RoundType.Knockouts && draw?.DrawKnockouts != null && draw.DrawKnockouts.Count > 0)
            {
                computedResults = ComputeKnockoutResults(draw.DrawKnockouts, category);
            }
            else if (category.RoundType == RoundType.Pools && draw?.DrawPools != null && draw.DrawPools.Count > 0)
            {
                computedResults = ComputePoolResults(draw.DrawPools, category);
            }
            else
            {
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

            return computedResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur calcul des résultats pour la catégorie {CategoryId}", category.Id);
            return new List<CategoryCompetitorResultModel>();
        }
    }

    private static string GetCategoryTypeLabel(CategoryModel category)
    {
        return category.Type switch
        {
            CategoryType.Taolu => "Taolu",
            CategoryType.TaoluModerneNord => "Taolu Moderne Nord",
            CategoryType.TaoluModerneSud => "Taolu Moderne Sud",
            _ => category.RoundType switch
            {
                RoundType.Knockouts => "Sanda - Élimination directe",
                RoundType.Pools => "Sanda - Poules",
                RoundType.Order => "Taolu",
                _ => "Sanda"
            }
        };
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !invalid.Contains(c)).ToArray()).Replace(' ', '_');
    }
    #endregion
}
