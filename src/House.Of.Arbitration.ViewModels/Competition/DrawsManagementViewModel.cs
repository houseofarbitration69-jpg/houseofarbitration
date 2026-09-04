#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
#endregion

namespace House.Of.Arbitration.ViewModels.Competition;

[QueryProperty(nameof(CompetitionId), "CompetitionId")]
public partial class DrawsManagementViewModel : BaseViewModel
{
    #region Services
    private readonly IRepository<CompetitionModel> _competitionRepository;
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
    public int CompetitionId
    {
        get => _competitionId;
        set
        {
            if (SetProperty(ref _competitionId, value))
            {
                MainThread.BeginInvokeOnMainThread(async () => await LoadCompetitionAsync());
            }
        }
    }

    public CompetitionModel? Competition
    {
        get => _competition;
        set => SetProperty(ref _competition, value);
    }

    public ObservableCollection<object> DisplayItems
    {
        get => _displayItems;
        set => SetProperty(ref _displayItems, value);
    }

    public int TotalMatchesCount
    {
        get => _totalMatchesCount;
        set => SetProperty(ref _totalMatchesCount, value);
    }

    public int TotalCategoriesCount
    {
        get => _totalCategoriesCount;
        set => SetProperty(ref _totalCategoriesCount, value);
    }

    public bool HasMatches => TotalMatchesCount > 0;
    #endregion

    #region Constructors
    public DrawsManagementViewModel(
        IPopupService popupService, 
        ILogger<DrawsManagementViewModel> logger, 
        ResourceProvider resourceProvider, 
        IRepository<CompetitionModel> competitionRepository,
        IRepository<DrawModel> drawRepository)
        : base(logger, resourceProvider, popupService)
    {
        _competitionRepository = competitionRepository;
        _drawRepository = drawRepository;
    }
    #endregion

    #region Private Methods
    private async Task LoadCompetitionAsync()
    {
        IsBusy = true;
        try
        {
            Competition = await _competitionRepository.GetByIdAsync(CompetitionId, 
                "Categories.AgeRange", 
                "Categories.Competitors.Competitor.Country");

            if (Competition == null || Competition.Categories == null) return;
            _categories = Competition.Categories.ToList();

            var allDraws = (await _drawRepository.GetAllAsync(
                "DrawKnockouts.Competitor1.Country",
                "DrawKnockouts.Competitor2.Country",
                "DrawKnockouts.Winner",
                "DrawKnockouts.Looser",
                "DrawPools.Competitor1.Country",
                "DrawPools.Competitor2.Country",
                "DrawPools.Winner",
                "DrawPools.Looser",
                "DrawOrders.Competitor.Country"))?.Where(d => _categories.Any(c => c.Id == d.CategoryId)).ToList();

            _allMatches.Clear();

            foreach (var category in _categories)
            {
                if (category.Type == CategoryType.Taolu)
                {
                    category.RoundType = RoundType.Order;
                }

                var existingDraw = allDraws?.FirstOrDefault(d => d.CategoryId == category.Id);

                if (existingDraw != null)
                {
                    if (category.RoundType == RoundType.Knockouts && existingDraw.DrawKnockouts != null && existingDraw.DrawKnockouts.Count > 0)
                    {
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
                                IsFinished = k.IsFinished
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

            // Sort matches: preserve existing GlobalOrder > 0, then by Category and local Order
            _allMatches = _allMatches
                .OrderBy(m => m.GlobalOrder > 0 ? 0 : 1)
                .ThenBy(m => m.GlobalOrder > 0 ? m.GlobalOrder : 0)
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

    private void GenerateMatchesForCategory(CategoryModel category)
    {
        if (category.Competitors == null || category.Competitors.Count == 0) return;

        if (category.RoundType == RoundType.Knockouts)
        {
            int n = category.Competitors.Count;
            int m = 1;
            while (m < n) m *= 2;
            if (m < 2) m = 2;

            int matchesInRound = m / 2;
            int matchOffset = 0;
            int currentMatches = matchesInRound;

            // Round 1
            for (int i = 0; i < matchesInRound; i++)
            {
                var comp1 = (i * 2 < n) ? category.Competitors[i * 2].Competitor : null;
                var comp2 = (i * 2 + 1 < n) ? category.Competitors[i * 2 + 1].Competitor : null;
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
                    IsBye = (compCount == 1)
                });
            }

            matchOffset += matchesInRound;
            while (currentMatches > 1)
            {
                currentMatches /= 2;
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
                        GlobalOrder = 0
                    });
                }
                matchOffset += currentMatches;
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

    [RelayCommand]
    private async Task ScrollToOrder()
    {
        if (_allMatches.Count == 0) return;

        string? result = await Shell.Current.DisplayPromptAsync(
            "Accéder à un ordre",
            $"Entrez le numéro d'ordre de passage (1 à {_allMatches.Count}) :",
            "Atteindre",
            "Annuler",
            placeholder: "1",
            keyboard: Keyboard.Numeric);

        if (!string.IsNullOrWhiteSpace(result) && int.TryParse(result, out int targetOrder))
        {
            ScrollToTargetOrder(targetOrder);
        }
    }

    [RelayCommand]
    private void ScrollToTargetOrder(object? param)
    {
        if (param == null || _allMatches.Count == 0) return;

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

    [RelayCommand]
    private async Task MoveDown(DrawMatchItemViewModel match)
    {
        if (match == null) return;
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

    [RelayCommand]
    private async Task EditOrder(DrawMatchItemViewModel match)
    {
        if (match == null || _allMatches.Count == 0) return;

        try
        {
            var parameters = new Dictionary<string, object>
            {
                { "Match", match },
                { "TotalMatches", _allMatches.Count }
            };

            var popupResult = await _popupService.ShowPopupAsync<ChangeOrderPopupViewModel, int?>(Shell.Current, shellParameters: parameters);

            if (popupResult != null && popupResult.Result.HasValue)
            {
                int newOrder = popupResult.Result.Value;
                int currentIndex = _allMatches.IndexOf(match);
                if (currentIndex < 0) return;

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

                if (categoryMatches.Count == 0 && finishedKnockouts.Count == 0 && finishedPools.Count == 0 && finishedOrders.Count == 0) continue;

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

    [RelayCommand]
    private async Task Close()
    {
        await Shell.Current.GoToAsync("..");
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

    public string MatchTitle
    {
        get
        {
            if (RoundType == RoundType.Order)
                return $"Passage #{Order}";
            if (RoundType == RoundType.Pools)
                return $"Poule - Match #{Order}";
            return $"Combat #{Order}";
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
