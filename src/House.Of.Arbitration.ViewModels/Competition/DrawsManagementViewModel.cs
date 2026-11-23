#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Models.Helpers;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
#endregion

namespace House.Of.Arbitration.ViewModels.Competition;

[QueryProperty(nameof(CompetitionId), Constants.Navigation.Parameter.COMPETITION_ID)]
public partial class DrawsManagementViewModel : BaseViewModel, IQueryAttributable
{
    #region Services
    /// <summary>
    /// Service d'accès aux données pour les compétitions
    /// </summary>
    private readonly IRepository<CompetitionModel> _competitionRepository;
    
    /// <summary>
    /// Service d'accès aux données pour les catégories
    /// </summary>
    private readonly IRepository<CategoryModel> _categoryRepository;
    
    /// <summary>
    /// Service d'accès aux données pour les tirages
    /// </summary>
    private readonly IRepository<DrawModel> _drawRepository;
    #endregion

    #region Attributs
    private int _competitionId;
    private CompetitionModel? _competition;
    private List<CategoryModel> _categories = new();
    private List<DrawMatchItemViewModel> _allMatches = new();
    private ObservableCollection<object> _displayItems = new();
    private int _totalMatchesCount;
    private int _totalCategoriesCount;
    #endregion

    #region Properties
    /// <summary>
    /// Obtient ou définit l'identifiant de la compétition
    /// </summary>
    public int CompetitionId
    {
        get => _competitionId;
        set => SetProperty(ref _competitionId, value);
    }

    /// <summary>
    /// Obtient ou définit la compétition
    /// </summary>
    public CompetitionModel? Competition
    {
        get => _competition;
        set => SetProperty(ref _competition, value);
    }

    /// <summary>
    /// Obtient ou définit la liste des items a afficher
    /// </summary>
    public ObservableCollection<object> DisplayItems
    {
        get => _displayItems;
        set => SetProperty(ref _displayItems, value);
    }

    /// <summary>
    /// Obtient ou définit le nombre total de rencontre
    /// </summary>
    public int TotalMatchesCount
    {
        get => _totalMatchesCount;
        set => SetProperty(ref _totalMatchesCount, value);
    }

    /// <summary>
    /// Obtient ou définit le nombre total de catégories
    /// </summary>
    public int TotalCategoriesCount
    {
        get => _totalCategoriesCount;
        set => SetProperty(ref _totalCategoriesCount, value);
    }

    /// <summary>
    /// Indique s'il reste au moins une rencontre
    /// </summary>
    public bool HasMatches => TotalMatchesCount > 0;
    #endregion

    #region Constructors
    /// <summary>
    /// Constructeur permettant d'initialiser les services
    /// </summary>
    /// <param name="popupService">Service de gestion des popups</param>
    /// <param name="logger">Service des logs</param>
    /// <param name="resourceProvider">Service de traduction</param>
    /// <param name="competitionRepository">Service d'accès aux données pour les compétitions</param>
    /// <param name="categoryRepository">Service d'accès aux données pour les catégories</param>
    /// <param name="drawRepository">Services d'accès aux données pour les tirages</param>
    public DrawsManagementViewModel(
        IPopupService popupService,
        ILogger<DrawsManagementViewModel> logger,
        ResourceProvider resourceProvider,
        IRepository<CompetitionModel> competitionRepository,
        IRepository<CategoryModel> categoryRepository,
        IRepository<DrawModel> drawRepository)
        : base(logger, resourceProvider, popupService)
    {
        _competitionRepository = competitionRepository;
        _categoryRepository = categoryRepository;
        _drawRepository = drawRepository;
    }
    #endregion

    #region Implement IQueryAttributable
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(nameof(CompetitionId), out var compIdObj) || query.TryGetValue(Constants.Navigation.Parameter.COMPETITION_ID, out compIdObj))
        {
            int newId = 0;

            if (compIdObj is int id)
            {
                newId = id;
            }
            else if (int.TryParse(compIdObj?.ToString(), out int parsedId))
            {
                newId = parsedId;
            }

            CompetitionId = newId;

            MainThread.BeginInvokeOnMainThread(async () => await LoadCompetitionAsync());
        }
    }
    #endregion

    #region Override Methods
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override async Task OnAppearing()
    {
        await base.OnAppearing();

        if (CompetitionId > 0 && (_categories == null || _categories.Count == 0 || _allMatches.Count == 0))
        {
            await LoadCompetitionAsync();
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Méthode permettant de récupérer les données de la compétition
    /// </summary>
    /// <returns></returns>
    private async Task LoadCompetitionAsync()
    {
        if (CompetitionId <= 0)
        {
            return;
        }

        IsBusy = true;
        try
        {
            Competition = await _competitionRepository.GetByIdAsync(CompetitionId,
                "Categories.AgeRange",
                "Categories.Competitors.Competitor.Country");

            var allCategories = await _categoryRepository.GetAllAsync(
                "AgeRange",
                "Competitors.Competitor.Country",
                "Competitors.Warnings");

            var compCategories = allCategories?.Where(c => c.CompetitionId == CompetitionId).ToList() ?? new List<CategoryModel>();

            if (Competition != null)
            {
                Competition.Categories = compCategories;
            }

            _categories = compCategories;

            if (_categories.Count == 0)
            {
                _allMatches.Clear();
                RefreshDisplayList();
                return;
            }

            var categoryIds = _categories.Select(c => c.Id).ToHashSet();

            var allDraws = (await _drawRepository.GetAllAsync(
                "DrawKnockouts.Competitor1.Country",
                "DrawKnockouts.Competitor2.Country",
                "DrawKnockouts.Winner",
                "DrawKnockouts.Looser",
                "DrawPools.Competitor1.Country",
                "DrawPools.Competitor2.Country",
                "DrawPools.Winner",
                "DrawPools.Looser",
                "DrawOrders.Competitor.Country"))?.Where(d => categoryIds.Contains(d.CategoryId)).ToList();

            _allMatches.Clear();

            foreach (var category in _categories)
            {
                if (category.Type == CategoryType.Taolu || category.Type == CategoryType.TaoluModerneNord || category.Type == CategoryType.TaoluModerneSud)
                {
                    category.RoundType = RoundType.Order;
                }

                var existingDraw = allDraws?.FirstOrDefault(d => d.CategoryId == category.Id);

                if (existingDraw != null)
                {
                    if (category.RoundType == RoundType.Knockouts && existingDraw.DrawKnockouts != null && existingDraw.DrawKnockouts.Count > 0)
                    {
                        int totalMatches = existingDraw.DrawKnockouts.Count;
                        foreach (var k in existingDraw.DrawKnockouts.Where(x => !x.IsFinished).OrderBy(x => x.Order))
                        {
                            _allMatches.Add(new DrawMatchItemViewModel
                            {
                                DrawModel = k,
                                CategoryId = category.Id,
                                CategoryName = category.Name,
                                Category = category,
                                RoundType = RoundType.Knockouts,
                                Order = k.Order,
                                GlobalOrder = k.GlobalOrder,
                                Competitor1 = k.Competitor1,
                                Competitor2 = k.Competitor2,
                                IsFinished = k.IsFinished,
                                TotalMatchesInCategory = totalMatches
                            });
                        }
                    }
                    else if (category.RoundType == RoundType.Pools && existingDraw.DrawPools != null && existingDraw.DrawPools.Count > 0)
                    {
                        foreach (var p in existingDraw.DrawPools.Where(x => !x.IsFinished).OrderBy(x => x.Order))
                        {
                            _allMatches.Add(new DrawMatchItemViewModel
                            {
                                DrawModel = p,
                                CategoryId = category.Id,
                                CategoryName = category.Name,
                                Category = category,
                                RoundType = RoundType.Pools,
                                Order = p.Order,
                                GlobalOrder = p.GlobalOrder,
                                Competitor1 = p.Competitor1,
                                Competitor2 = p.Competitor2,
                                IsFinished = p.IsFinished
                            });
                        }
                    }
                    else if (category.RoundType == RoundType.Order && existingDraw.DrawOrders != null && existingDraw.DrawOrders.Count > 0)
                    {
                        foreach (var o in existingDraw.DrawOrders.Where(x => !x.IsFinished).OrderBy(x => x.Order))
                        {
                            _allMatches.Add(new DrawMatchItemViewModel
                            {
                                DrawModel = o,
                                CategoryId = category.Id,
                                CategoryName = category.Name,
                                Category = category,
                                RoundType = RoundType.Order,
                                Order = o.Order,
                                GlobalOrder = o.GlobalOrder,
                                Competitor = o.Competitor,
                                IsFinished = o.IsFinished
                            });
                        }
                    }
                    else
                    {
                        GenerateMatchesForCategory(category);
                    }
                }
                else
                {
                    GenerateMatchesForCategory(category);
                }
            }

            // Sort matches: Phase (prelims: 0, petite finale: 1, finale: 2), then GlobalOrder (if > 0), Category and local Order
            _allMatches = _allMatches
                .OrderBy(m => GetMatchPhasePriority(m))
                .ThenBy(m => m.GlobalOrder > 0 ? m.GlobalOrder : int.MaxValue)
                .ThenBy(m => m.CategoryId)
                .ThenBy(m => m.Order)
                .ToList();

            RefreshDisplayList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading competition draws for competition {Id}", CompetitionId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Méthode permettant de générer les rencontres d'une catégorie
    /// </summary>
    /// <param name="category">Catégorie dont on veut les rencontres</param>
    private void GenerateMatchesForCategory(CategoryModel category)
    {
        if (category.Competitors == null || category.Competitors.Count == 0)
        {
            return;
        }

        if (category.RoundType == RoundType.Knockouts)
        {
            int n = category.Competitors.Count;

            // Cas particulier : 1 seul compétiteur → passage direct en finale automatique
            if (n == 1)
            {
                var soleCompetitor = category.Competitors[0].Competitor;
                var k = new DrawKnockoutModel
                {
                    Order = 1,
                    GlobalOrder = 0,
                    Competitor1 = soleCompetitor,
                    Competitor1Id = soleCompetitor?.Id,
                    Competitor2Id = null,
                    IsFinished = true,
                    WinnerId = soleCompetitor?.Id
                };
                _allMatches.Add(new DrawMatchItemViewModel
                {
                    DrawModel = k,
                    CategoryId = category.Id,
                    CategoryName = category.Name,
                    Category = category,
                    RoundType = RoundType.Knockouts,
                    Order = 1,
                    GlobalOrder = 0,
                    Competitor1 = soleCompetitor,
                    IsFinished = true,
                    IsBye = true,
                    TotalMatchesInCategory = 1
                });
                return;
            }

            int m = 1;

            while (m < n)
            {
                m *= 2;
            }

            if (m < 2)
            {
                m = 2;
            }
            int totalMatches = (category.HasBronzeMatch && m >= 4) ? m : (m - 1);

            int matchesInRound = m / 2;
            int matchOffset = 0;
            int currentMatches = matchesInRound;

            var competitorList = category.Competitors.Select(c => c.Competitor).Where(c => c != null).ToList()!;
            var pairs = KnockoutStageHelper.DistributeFirstRoundCompetitors(competitorList!, matchesInRound);

            // Round 1 — on mémorise les vainqueurs de bye (index de match → compétiteur)
            var byeWinners = new Dictionary<int, CompetitorModel>(); // key = index match (0-based dans le tour 1)
            int round1StartIdx = _allMatches.Count; // index global dans _allMatches du premier match du tour 1

            for (int i = 0; i < matchesInRound; i++)
            {
                var (comp1, comp2) = i < pairs.Count ? pairs[i] : (null, null);
                int compCount = (comp1 != null ? 1 : 0) + (comp2 != null ? 1 : 0);

                var k = new DrawKnockoutModel
                {
                    Order = i + 1,
                    GlobalOrder = 0,
                    Competitor1 = comp1,
                    Competitor2 = comp2,
                    Competitor1Id = comp1?.Id,
                    Competitor2Id = comp2?.Id
                };

                if (compCount == 1 && comp1 != null)
                {
                    k.WinnerId = comp1.Id;
                    k.IsFinished = true;
                    byeWinners[i] = comp1; // mémorisé pour propagation vers tour 2
                }

                _allMatches.Add(new DrawMatchItemViewModel
                {
                    DrawModel = k,
                    CategoryId = category.Id,
                    CategoryName = category.Name,
                    Category = category,
                    RoundType = RoundType.Knockouts,
                    Order = k.Order,
                    GlobalOrder = 0,
                    Competitor1 = comp1,
                    Competitor2 = comp2,
                    IsFinished = (compCount == 1),
                    IsBye = (compCount == 1),
                    TotalMatchesInCategory = totalMatches
                });
            }

            matchOffset += matchesInRound;
            while (currentMatches > 1)
            {
                currentMatches /= 2;

                if (currentMatches == 1 && category.HasBronzeMatch && m >= 4)
                {
                    // Petite finale avant la finale
                    var bronzeK = new DrawKnockoutModel
                    {
                        Order = matchOffset + 1,
                        GlobalOrder = 0,
                        IsBronzeMatch = true
                    };

                    _allMatches.Add(new DrawMatchItemViewModel
                    {
                        DrawModel = bronzeK,
                        CategoryId = category.Id,
                        CategoryName = category.Name,
                        Category = category,
                        RoundType = RoundType.Knockouts,
                        Order = bronzeK.Order,
                        GlobalOrder = 0,
                        TotalMatchesInCategory = totalMatches
                    });

                    // Finale
                    var finalK = new DrawKnockoutModel
                    {
                        Order = matchOffset + 2,
                        GlobalOrder = 0,
                        IsBronzeMatch = false
                    };

                    _allMatches.Add(new DrawMatchItemViewModel
                    {
                        DrawModel = finalK,
                        CategoryId = category.Id,
                        CategoryName = category.Name,
                        Category = category,
                        RoundType = RoundType.Knockouts,
                        Order = finalK.Order,
                        GlobalOrder = 0,
                        TotalMatchesInCategory = totalMatches
                    });

                    matchOffset += 2;
                }
                else
                {
                    for (int i = 0; i < currentMatches; i++)
                    {
                        var k = new DrawKnockoutModel
                        {
                            Order = matchOffset + i + 1,
                            GlobalOrder = 0
                        };

                        _allMatches.Add(new DrawMatchItemViewModel
                        {
                            DrawModel = k,
                            CategoryId = category.Id,
                            CategoryName = category.Name,
                            Category = category,
                            RoundType = RoundType.Knockouts,
                            Order = k.Order,
                            GlobalOrder = 0,
                            TotalMatchesInCategory = totalMatches
                        });
                    }
                    matchOffset += currentMatches;
                }
            }

            // Propager les vainqueurs des byes du tour 1 vers le tour 2.
            // Le match i (0-based) du tour 1 alimente :
            //   → Competitor1 si i est pair, Competitor2 si i est impair
            //   → du match i/2 (0-based) du tour 2 (index global = round1StartIdx + matchesInRound + i/2)
            if (byeWinners.Count > 0)
            {
                int round2StartIdx = round1StartIdx + matchesInRound;
                foreach (var kvp in byeWinners)
                {
                    int matchIdx = kvp.Key;
                    var winner = kvp.Value;
                    int nextMatchGlobalIdx = round2StartIdx + matchIdx / 2;

                    if (nextMatchGlobalIdx >= _allMatches.Count) continue;

                    var nextMatch = _allMatches[nextMatchGlobalIdx];
                    if (matchIdx % 2 == 0)
                    {
                        nextMatch.Competitor1 = winner;
                        if (nextMatch.DrawModel is DrawKnockoutModel nextKo1)
                        {
                            nextKo1.Competitor1 = winner;
                            nextKo1.Competitor1Id = winner.Id;
                        }
                    }
                    else
                    {
                        nextMatch.Competitor2 = winner;
                        if (nextMatch.DrawModel is DrawKnockoutModel nextKo2)
                        {
                            nextKo2.Competitor2 = winner;
                            nextKo2.Competitor2Id = winner.Id;
                        }
                    }
                }
            }
        }
        else if (category.RoundType == RoundType.Pools)
        {
            int n = category.Competitors.Count;
            int matchOrder = 1;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    var comp1 = category.Competitors[i].Competitor;
                    var comp2 = category.Competitors[j].Competitor;
                    var p = new DrawPoolsModel
                    {
                        Order = matchOrder++,
                        GlobalOrder = 0,
                        Competitor1 = comp1,
                        Competitor2 = comp2,
                        Competitor1Id = comp1?.Id,
                        Competitor2Id = comp2?.Id
                    };

                    _allMatches.Add(new DrawMatchItemViewModel
                    {
                        DrawModel = p,
                        CategoryId = category.Id,
                        CategoryName = category.Name,
                        Category = category,
                        RoundType = RoundType.Pools,
                        Order = p.Order,
                        GlobalOrder = 0,
                        Competitor1 = comp1,
                        Competitor2 = comp2
                    });
                }
            }
        }
        else if (category.RoundType == RoundType.Order)
        {
            int n = category.Competitors.Count;
            for (int i = 0; i < n; i++)
            {
                var comp = category.Competitors[i].Competitor;
                var o = new DrawOrderModel
                {
                    Order = i + 1,
                    GlobalOrder = 0,
                    Competitor = comp,
                    CompetitorId = comp?.Id
                };

                _allMatches.Add(new DrawMatchItemViewModel
                {
                    DrawModel = o,
                    CategoryId = category.Id,
                    CategoryName = category.Name,
                    Category = category,
                    RoundType = RoundType.Order,
                    Order = o.Order,
                    GlobalOrder = 0,
                    Competitor = comp
                });
            }
        }
    }

    /// <summary>
    /// Méthode permettant de rafraichir la liste
    /// </summary>
    private void RefreshDisplayList()
    {
        // Re-index all matches sequentially: 1, 2, 3, ..., N and wire commands
        for (int i = 0; i < _allMatches.Count; i++)
        {
            var match = _allMatches[i];
            match.GlobalOrder = i + 1;
            match.DrawModel.GlobalOrder = i + 1;
            match.OnMoveUp = MoveUp;
            match.OnMoveDown = MoveDown;
            match.OnEditOrder = EditOrder;
        }

        var items = new List<object>();
        int lastCategoryId = -1;

        foreach (var match in _allMatches)
        {
            if (match.CategoryId != lastCategoryId)
            {
                items.Add(new DrawCategoryHeaderItem
                {
                    CategoryId = match.CategoryId,
                    CategoryName = match.CategoryName,
                    Category = match.Category,
                    Details = $"{match.Category?.Type} • {match.Category?.AgeRange?.Label} • {match.Category?.Genre}"
                });
                lastCategoryId = match.CategoryId;
            }
            items.Add(match);
        }

        DisplayItems = new ObservableCollection<object>(items);

        TotalMatchesCount = _allMatches.Count;
        TotalCategoriesCount = _categories.Count;
        OnPropertyChanged(nameof(TotalMatchesCount));
        OnPropertyChanged(nameof(TotalCategoriesCount));
        OnPropertyChanged(nameof(HasMatches));
    }
    #endregion

    #region Scroll Support
    public event Action<object>? ScrollToItemRequested;
    public event Action<int>? ScrollToRequested;

    /// <summary>
    /// Commande permettant d'aller a un ordre précis
    /// </summary>
    [RelayCommand]
    private async Task ScrollToOrder()
    {
        if (_allMatches.Count == 0)
        {
            return;
        }

        
        string? result = await Shell.Current.DisplayPromptAsync(
            Resources.GO_TO_ORDER,
            $"{Resources.ENTER_ORDER_NUM} (1 à {_allMatches.Count}) :",
            Resources.GO_TO,
            Resources.CANCEL,
            placeholder: "1",
            keyboard: Keyboard.Numeric);

        if (!string.IsNullOrWhiteSpace(result) && int.TryParse(result, out int targetOrder))
        {
            ScrollToTargetOrder(targetOrder);
        }
    }

    /// <summary>
    /// Commande permettant d'aller un ordre précis
    /// </summary>
    /// <param name="param"></param>
    [RelayCommand]
    private void ScrollToTargetOrder(object? param)
    {
        if (param == null || _allMatches.Count == 0)
        {
            return;
        }

        int targetOrder = 1;
        if (param is int i)
        {
            targetOrder = i;
        }
        else if (int.TryParse(param.ToString(), out int parsed))
        {
            targetOrder = parsed;
        }

        int clampedOrder = Math.Clamp(targetOrder, 1, _allMatches.Count);
        // Find corresponding match item in DisplayItems
        var targetMatch = _allMatches.FirstOrDefault(m => m.GlobalOrder == clampedOrder);
        if (targetMatch != null)
        {
            int displayIndex = DisplayItems.IndexOf(targetMatch);
            if (displayIndex >= 0)
            {
                ScrollToRequested?.Invoke(displayIndex);
            }
            ScrollToItemRequested?.Invoke(targetMatch);
        }
    }
    #endregion

    #region Commands
    /// <summary>
    /// Commande pour déplacer la rencontre vers le haut
    /// </summary>
    [RelayCommand]
    private async Task MoveUp(DrawMatchItemViewModel match)
    {
        if (match == null) return;
        int index = _allMatches.IndexOf(match);
        if (index > 0)
        {
            IsBusy = true;
            await Task.Yield();
            try
            {
                _allMatches.RemoveAt(index);
                _allMatches.Insert(index - 1, match);
                RefreshDisplayList();
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>
    /// Commande pour déplcaer la rencontre vers le base
    /// </summary>
    [RelayCommand]
    private async Task MoveDown(DrawMatchItemViewModel match)
    {
        if (match == null)
        {
            return;
        }

        int index = _allMatches.IndexOf(match);
        if (index >= 0 && index < _allMatches.Count - 1)
        {
            IsBusy = true;
            await Task.Yield();

            try
            {
                _allMatches.RemoveAt(index);
                _allMatches.Insert(index + 1, match);
                RefreshDisplayList();
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>
    /// Commander permettant d'editer l'ordre de la rencontre
    /// </summary>
    [RelayCommand]
    private async Task EditOrder(DrawMatchItemViewModel match)
    {
        if (match == null || _allMatches.Count == 0)
        {
            return;
        }

        try
        {
            var parameters = new Dictionary<string, object>
            {
                { "Match", match },
                { "TotalMatches", _allMatches.Count }
            };

            var popupResult = await _popupService.ShowPopupAsync<ChangeOrderPopupViewModel, int?>(
                Shell.Current,
                options: new PopupOptions() { Shadow = null },
                shellParameters: parameters
            );

            if (popupResult != null && popupResult.Result.HasValue)
            {
                int newOrder = popupResult.Result.Value;
                int currentIndex = _allMatches.IndexOf(match);
                
                if (currentIndex < 0)
                {
                    return;
                }

                int targetIndex = Math.Clamp(newOrder - 1, 0, _allMatches.Count - 1);
                if (targetIndex != currentIndex)
                {
                    IsBusy = true;
                    await Task.Yield();
                
                    try
                    {
                        _allMatches.RemoveAt(currentIndex);
                        _allMatches.Insert(targetIndex, match);
                        RefreshDisplayList();
                        ScrollToTargetOrder(match.GlobalOrder);
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'affichage de la popup de changement d'ordre");
        }
    }

    /// <summary>
    /// Commande permettant de sauvegarder l'ordre des rencontres
    /// </summary>
    [RelayCommand]
    private async Task Save()
    {
        IsBusy = true;
        try
        {
            _drawRepository.ClearTracker();

            var existingDraws = (await _drawRepository.GetAllAsync(
                "DrawKnockouts",
                "DrawPools",
                "DrawOrders"))?.Where(d => _categories.Any(c => c.Id == d.CategoryId)).ToList();

            if (existingDraws != null)
            {
                foreach (var ed in existingDraws)
                {
                    ed.Category = null;
                    ed.DrawKnockouts = null;
                    ed.DrawPools = null;
                    ed.DrawOrders = null;
                    await _drawRepository.DeleteAsync(ed);
                }
            }

            foreach (var category in _categories)
            {
                var categoryMatches = _allMatches.Where(m => m.CategoryId == category.Id).ToList();
                var existingDraw = existingDraws?.FirstOrDefault(d => d.CategoryId == category.Id);

                var finishedKnockouts = existingDraw?.DrawKnockouts?.Where(k => k.IsFinished).ToList() ?? new List<DrawKnockoutModel>();
                var finishedPools = existingDraw?.DrawPools?.Where(p => p.IsFinished).ToList() ?? new List<DrawPoolsModel>();
                var finishedOrders = existingDraw?.DrawOrders?.Where(o => o.IsFinished).ToList() ?? new List<DrawOrderModel>();

                if (categoryMatches.Count == 0 && finishedKnockouts.Count == 0 && finishedPools.Count == 0 && finishedOrders.Count == 0)
                {
                    continue;
                }

                var draw = new DrawModel
                {
                    CategoryId = category.Id,
                    DrawKnockouts = new List<DrawKnockoutModel>(),
                    DrawPools = new List<DrawPoolsModel>(),
                    DrawOrders = new List<DrawOrderModel>()
                };

                // Add finished matches back so they are not deleted/lost
                foreach (var k in finishedKnockouts)
                {
                    draw.DrawKnockouts.Add(new DrawKnockoutModel
                    {
                        Draw = draw,
                        Order = k.Order,
                        GlobalOrder = k.GlobalOrder,
                        Competitor1Id = k.Competitor1Id,
                        Competitor2Id = k.Competitor2Id,
                        WinnerId = k.WinnerId,
                        LooserId = k.LooserId,
                        IsBronzeMatch = k.IsBronzeMatch,
                        IsFinished = true
                    });
                }

                foreach (var p in finishedPools)
                {
                    draw.DrawPools.Add(new DrawPoolsModel
                    {
                        Draw = draw,
                        Order = p.Order,
                        GlobalOrder = p.GlobalOrder,
                        Competitor1Id = p.Competitor1Id,
                        Competitor2Id = p.Competitor2Id,
                        WinnerId = p.WinnerId,
                        LooserId = p.LooserId,
                        IsFinished = true
                    });
                }

                foreach (var o in finishedOrders)
                {
                    draw.DrawOrders.Add(new DrawOrderModel
                    {
                        Draw = draw,
                        Order = o.Order,
                        GlobalOrder = o.GlobalOrder,
                        CompetitorId = o.CompetitorId,
                        IsFinished = true
                    });
                }

                // Add unfinished (reordered) matches
                foreach (var match in categoryMatches)
                {
                    if (match.RoundType == RoundType.Knockouts)
                    {
                        var k = new DrawKnockoutModel
                        {
                            Draw = draw,
                            Order = match.Order,
                            GlobalOrder = match.GlobalOrder,
                            Competitor1Id = match.Competitor1?.Id,
                            Competitor2Id = match.Competitor2?.Id,
                            IsBronzeMatch = (match.DrawModel as DrawKnockoutModel)?.IsBronzeMatch ?? false,
                            IsFinished = match.IsFinished
                        };

                        if (match.IsBye)
                        {
                            k.WinnerId = match.Competitor1?.Id ?? match.Competitor2?.Id;
                            k.IsFinished = true;
                        }
                        
                        draw.DrawKnockouts.Add(k);
                    }
                    else if (match.RoundType == RoundType.Pools)
                    {
                        draw.DrawPools.Add(new DrawPoolsModel
                        {
                            Draw = draw,
                            Order = match.Order,
                            GlobalOrder = match.GlobalOrder,
                            Competitor1Id = match.Competitor1?.Id,
                            Competitor2Id = match.Competitor2?.Id,
                            IsFinished = match.IsFinished
                        });
                    }
                    else if (match.RoundType == RoundType.Order)
                    {
                        draw.DrawOrders.Add(new DrawOrderModel
                        {
                            Draw = draw,
                            Order = match.Order,
                            GlobalOrder = match.GlobalOrder,
                            CompetitorId = match.Competitor?.Id,
                            IsFinished = match.IsFinished
                        });
                    }
                }

                _drawRepository.ClearTracker();
                await _drawRepository.AddAsync(draw);
            }

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Snackbar.Make("L'ordre des passages et les tirages ont été enregistrés avec succès.").Show();
            });

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la sauvegarde des tirages");
            await Shell.Current.DisplayAlertAsync("Erreur", "Impossible d'enregistrer l'ordre des combats.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Commande permettant de fermer l'edition des ordres des rencontres
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task Close()
    {
        await Shell.Current.GoToAsync("..");
    }

    private static int GetMatchPhasePriority(DrawMatchItemViewModel m)
    {
        if (m.DrawModel is DrawKnockoutModel k)
        {
            if (k.IsBronzeMatch) return 1;
            if (k.StageName == "Finale") return 2;
            return 0;
        }
        return 0;
    }
    #endregion
}

public class DrawCategoryHeaderItem
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public CategoryModel? Category { get; set; }
}

public partial class DrawMatchItemViewModel : ObservableObject
{
    private int _globalOrder;
    private bool _isFinished;

    public IDrawModel DrawModel { get; set; } = null!;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public CategoryModel? Category { get; set; }
    public RoundType RoundType { get; set; }
    public int Order { get; set; }

    public int GlobalOrder
    {
        get => _globalOrder;
        set => SetProperty(ref _globalOrder, value);
    }

    public bool IsFinished
    {
        get => _isFinished;
        set => SetProperty(ref _isFinished, value);
    }

    public bool IsBye { get; set; }

    public CompetitorModel? Competitor1 { get; set; }
    public CompetitorModel? Competitor2 { get; set; }
    public CompetitorModel? Competitor { get; set; }

    public bool IsDuel => RoundType != RoundType.Order;
    public bool IsSingle => RoundType == RoundType.Order;
    public int TotalMatchesInCategory { get; set; }

    public string MatchTitle
    {
        get
        {
            if (RoundType == RoundType.Order)
            {
                //return $"Passage #{Order}";
                return $"Passage";
            }

            if (RoundType == RoundType.Pools)
            {
                //return $"Poule - Match #{Order}";
                return $"Poule - Match";
            }

            if (DrawModel is DrawKnockoutModel ko)
            {
                return ko.StageName;
            }

            return KnockoutStageHelper.GetStageName(Order, TotalMatchesInCategory, false);
        }
    }

    public Func<DrawMatchItemViewModel, Task>? OnMoveUp { get; set; }
    public Func<DrawMatchItemViewModel, Task>? OnMoveDown { get; set; }
    public Func<DrawMatchItemViewModel, Task>? OnEditOrder { get; set; }

    [RelayCommand]
    private async Task MoveUp()
    {
        if (OnMoveUp != null)
        {
            await OnMoveUp.Invoke(this);
        }
    }

    [RelayCommand]
    private async Task MoveDown()
    {
        if (OnMoveDown != null)
        {
            await OnMoveDown.Invoke(this);
        }
    }

    [RelayCommand]
    private async Task EditOrder()
    {
        if (OnEditOrder != null)
        {
            await OnEditOrder.Invoke(this);
        }
    }
}
