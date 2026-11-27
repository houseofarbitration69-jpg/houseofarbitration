#region Imports
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
#endregion

namespace House.Of.Arbitration.ViewModels;

[QueryProperty(nameof(CompetitionId), Constants.Navigation.Parameter.COMPETITION_ID)]
[QueryProperty(nameof(CurrentCategoryId), Constants.Navigation.Parameter.CATEGORY_ID)]
public partial class HistoricViewModel : BaseViewModel, IQueryAttributable
{
    #region Services
    private readonly IRepository<RefereeDataModel> _refereeDataRepository;
    private readonly IRepository<CompetitionModel> _competitionRepository;
    private readonly IRepository<CategoryModel> _categoryRepository;
    private readonly IExcelImportExportService _excelService;
    private readonly IFileDownloadService _fileDownloadService;
    #endregion

    #region Attributs
    private int _competitionId;
    private int _currentCategoryId;
    private string _competitionName = string.Empty;

    private ObservableCollection<RefereeDataModel> _refereeDatas = new();
    private ObservableCollection<CompetitionModel> _competitions = new();
    private ObservableCollection<CategoryModel> _categories = new();
    private CompetitionModel? _selectedCompetition;
    private CategoryModel? _selectedCategory;
    private List<RefereeDataModel> _allDatas = new();
    private List<CategoryModel> _allCategories = new();
    private int? _targetCompetitionId;

    private bool _isLoadingCategories;

    private CompetitionModel? _competition;
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

    public string CompetitionName
    {
        get => _competitionName;
        set => SetProperty(ref _competitionName, value);
    }

    public ObservableCollection<RefereeDataModel> RefereeDatas
    {
        get => _refereeDatas;
        set => SetProperty(ref _refereeDatas, value);
    }

    public ObservableCollection<CompetitionModel> Competitions
    {
        get => _competitions;
        set => SetProperty(ref _competitions, value);
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
                FilterDatas();
            }
        }
    }

    public CompetitionModel? Competition
    {
        get => _competition;
        set => SetProperty(ref _competition, value);
    }
    #endregion

    #region Constructors
    public HistoricViewModel(
        ILogger<RefereeDataViewModel> logger,
        ResourceProvider resourceProvider,
        CommunityToolkit.Maui.IPopupService popupService,
        IRepository<RefereeDataModel> refereeDataRepository,
        IRepository<CompetitionModel> competitionRepository,
        IRepository<CategoryModel> categoryRepository,
        IExcelImportExportService excelService,
        IFileDownloadService fileDownloadService
    ) : base(logger, resourceProvider, popupService)
    {
        _refereeDataRepository = refereeDataRepository;
        _competitionRepository = competitionRepository;
        _categoryRepository = categoryRepository;
        _excelService = excelService;
        _fileDownloadService = fileDownloadService;
    }
    #endregion

    #region Implement IQueryAttributable
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(Constants.Navigation.Parameter.COMPETITION_ID, out var compIdObj) ||
            query.TryGetValue("CompetitionId", out compIdObj))
        {
            if (compIdObj is int id)
            {
                _targetCompetitionId = id;
            }
            else if (int.TryParse(compIdObj?.ToString(), out int parsedId))
            {
                _targetCompetitionId = parsedId;
            }
        }

        if (query.TryGetValue(Constants.Navigation.Parameter.CATEGORY_ID, out var catObj) &&
            int.TryParse(catObj?.ToString(), out int catId))
        {
            CurrentCategoryId = catId;
        }

        if (Categories.Count == 0 && CompetitionId > 0)
        {
            MainThread.BeginInvokeOnMainThread(async () => await LoadCategoriesAsync());
        }
    }
    #endregion

    #region Override Methods
    public override async Task OnAppearing()
    {
        IsBusy = true;
        try
        {
            if (CompetitionId > 0)
            {
                Competition = await _competitionRepository.GetByIdAsync(CompetitionId);
            }

            if (Categories.Count == 0)
            {
                await LoadCategoriesAsync();
            }

            var datas = await _refereeDataRepository.GetAllAsync(
                "DrawKnockoutModel.Draw.Category",
                "DrawOrder.Draw.Category",
                "DrawPools.Draw.Category",
                "Competitor");

            if (datas != null)
            {
                _allDatas = datas
                    .OrderBy(d => d.DrawKnockoutModel?.Draw?.Category?.Name)
                    .ThenBy(d => d.DrawOrder?.Draw?.Category?.Name)
                    .ThenBy(d => d.DrawPools?.Draw?.Category?.Name)
                    .ThenBy(d => d.DrawOrder?.CurrentRound)
                    .ToList();
            }

            FilterDatas();
        }
        finally
        {
            IsBusy = false;
        }
    }
    #endregion

    #region Private Methods
    [RelayCommand]
    private void ResetFilter()
    {
        _targetCompetitionId = null;
        SelectedCategory = null;
    }

    [RelayCommand]
    private async Task ExportToExcel()
    {
        if (_allDatas == null || !_allDatas.Any())
        {
            await Shell.Current.DisplayAlert("Export", "Aucune donnée à exporter.", "OK");
            return;
        }

        IsBusy = true;
        try
        {
            // Utiliser les données filtrées actuellement affichées
            var dataToExport = RefereeDatas.ToList();

            if (!dataToExport.Any())
            {
                await Shell.Current.DisplayAlert("Export", "Aucune donnée à exporter pour le filtre actuel.", "OK");
                return;
            }

            // Regrouper par catégorie
            var grouped = dataToExport
                .GroupBy(d => GetCategoryName(d))
                .OrderBy(g => g.Key)
                .Select(g => new CategoryRefereeExportData
                {
                    CategoryName = g.Key,
                    Rows = g.Select(d => new RefereeScoreExportRow
                    {
                        Date = d.Date,
                        MatchTimer = d.MatchTimer,
                        Referee = d.Referee ?? string.Empty,
                        CompetitorLastName = d.Competitor?.LastName ?? d.Competitor?.Name ?? string.Empty,
                        CompetitorFirstName = d.Competitor?.FirstName ?? string.Empty,
                        Score = d.Score,
                        Round = d.Round,
                        IsCorrection = d.IsCorrection,
                        MatchType = d.DrawKnockoutId.HasValue ? "Knockout"
                                  : d.DrawOrderId.HasValue ? "Taolu"
                                  : d.DrawPoolsId.HasValue ? "Poule"
                                  : "—",
                        MatchId = d.DrawKnockoutId ?? d.DrawOrderId ?? d.DrawPoolsId,
                        GlobalOrder = d.DrawKnockoutModel?.GlobalOrder
                                   ?? d.DrawOrder?.GlobalOrder
                                   ?? d.DrawPools?.GlobalOrder
                    }).ToList()
                })
                .ToList();

            //string competitionName = SelectedCompetition?.Name ?? "Toutes compétitions";
            //DateTime? competitionDate = SelectedCompetition?.Date;

            //var stream = await _excelService.ExportRefereeDataAsync(competitionName, competitionDate, grouped);

            var stream = await _excelService.ExportRefereeDataAsync(CompetitionName, null, grouped);

            string suffix = SelectedCategory != null ? $"_{SanitizeFileName(SelectedCategory.Name ?? "")}" : string.Empty;
            string fileName = $"NotesArbitres_{SanitizeFileName(CompetitionName)}{suffix}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

            string? savedPath = await _fileDownloadService.SaveToDownloadsAsync(
                stream, fileName,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

            if (savedPath != null)
                await Shell.Current.DisplayAlert("Export réussi", $"Fichier sauvegardé :\n{savedPath}", "OK");
            else
                await Shell.Current.DisplayAlert("Export", "Le fichier n'a pas pu être sauvegardé.", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'export Excel des notes arbitres");
            await Shell.Current.DisplayAlert("Erreur", "Une erreur est survenue lors de l'export Excel.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void FilterDatas()
    {
        var filtered = _allDatas.AsEnumerable();

        filtered = filtered.Where(d =>
            (d.DrawKnockoutModel?.Draw?.Category?.CompetitionId == CompetitionId) ||
            (d.DrawOrder?.Draw?.Category?.CompetitionId == CompetitionId) ||
            (d.DrawPools?.Draw?.Category?.CompetitionId == CompetitionId)
        );

        if (SelectedCategory != null)
        {
            filtered = filtered.Where(d =>
                (d.DrawKnockoutModel?.Draw?.CategoryId == SelectedCategory.Id) ||
                (d.DrawOrder?.Draw?.CategoryId == SelectedCategory.Id) ||
                (d.DrawPools?.Draw?.CategoryId == SelectedCategory.Id)
            );
        }

        RefereeDatas = new ObservableCollection<RefereeDataModel>(filtered.OrderByDescending(d => d.Date));
    }

    private static string GetCategoryName(RefereeDataModel d)
    {
        return d.DrawKnockoutModel?.Draw?.Category?.Name
            ?? d.DrawOrder?.Draw?.Category?.Name
            ?? d.DrawPools?.Draw?.Category?.Name
            ?? "Sans catégorie";
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !invalid.Contains(c)).ToArray()).Replace(' ', '_');
    }
    #endregion

    #region Load Data
    public async Task LoadCategoriesAsync()
    {
        if (_isLoadingCategories) return;

        _isLoadingCategories = true;

        try
        {
            IsBusy = true;
            if (CompetitionId > 0)
            {
                var comp = await _competitionRepository.GetByIdAsync(CompetitionId);
                if (comp != null) CompetitionName = comp.Name;
            }

            var allCats = await _categoryRepository.GetAllAsync("AgeRange");
            var filtered = allCats
                .Where(c => CompetitionId <= 0 || c.CompetitionId == CompetitionId)
                .OrderBy(c => c.Id)
                .ToList();

            Categories = new ObservableCollection<CategoryModel>(filtered);

            var defaultCat = filtered.FirstOrDefault(c => c.Id == CurrentCategoryId) ?? filtered.FirstOrDefault();
            SelectedCategory = defaultCat;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur chargement catégories dans ServerDrawsViewModel");
        }
        finally
        {
            _isLoadingCategories = false;
            IsBusy = false;
        }
    }
    #endregion
}
