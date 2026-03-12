#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

public partial class CompetitorsPageViewModel : BaseViewModel, IQueryAttributable
{
    #region Services
    private readonly IPopupService _popupService;
    #endregion

    #region Attributs
    private CategoryModel? _category;
    private ObservableCollection<CompetitorModel> _competitors = new();
    #endregion

    #region Properties    
    public CategoryModel? Category
    {
        get => _category;
        set
        {
            SetProperty(ref _category, value);
            if (value != null)
            {
                Competitors = new ObservableCollection<CompetitorModel>(value.Competitors ?? new());
                Competitors.CollectionChanged += OnCompetitorsCollectionChanged;
            }
        }
    }

    public ObservableCollection<CompetitorModel> Competitors
    {
        get => _competitors;
        set => SetProperty(ref _competitors, value);
    }
    #endregion

    #region Constructors
    public CompetitorsPageViewModel(IPopupService popupService, ILogger<CompetitorsPageViewModel> logger, ResourceProvider resourceProvider)
        : base(logger, resourceProvider)
    {
        _popupService = popupService;
    }
    #endregion

    #region Implement IQueryAttributable
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey(nameof(Category)))
        {
            Category = (CategoryModel?)query[nameof(Category)];
        }
    }
    #endregion

    #region Private Methods
    private void OnCompetitorsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Category != null)
        {
            Category.Competitors = Competitors.ToList();
        }
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task Close()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task AddCompetitor()
    {
        if (Category == null) return;

        var newCompetitor = new CompetitorModel
        {
            Genre = Category.Genre,
            BirthDate = DateTime.Now.AddYears(-20)
        };

        var queryAttributes = new Dictionary<string, object>
        {
            [nameof(CompetitorPopupViewModel.Competitor)] = newCompetitor,
            [nameof(CompetitorPopupViewModel.Category)] = Category,
        };

        var result = await _popupService.ShowPopupAsync<CompetitorPopupViewModel, CompetitorModel?>(Shell.Current, shellParameters: queryAttributes);

        if (result != null && result.Result != null)
        {
            Competitors.Add(result.Result);
        }
    }

    [RelayCommand]
    private async Task EditCompetitor(CompetitorModel competitor)
    {
        var queryAttributes = new Dictionary<string, object>
        {
            [nameof(CompetitorPopupViewModel.Competitor)] = competitor
        } as IDictionary<string, object>;

        var result = await _popupService.ShowPopupAsync<CompetitorPopupViewModel, CompetitorModel?>(Shell.Current, shellParameters: queryAttributes);

        if (result != null && result.Result != null)
        {
            var index = Competitors.IndexOf(competitor);
            if (index != -1)
            {
                Competitors[index] = null!;
                Competitors[index] = result.Result;
            }
        }
    }

    [RelayCommand]
    private void DeleteCompetitor(CompetitorModel competitor)
    {
        if (competitor != null && Competitors.Contains(competitor))
        {
            Competitors.Remove(competitor);
        }
    }
    #endregion
}
