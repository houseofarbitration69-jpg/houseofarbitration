#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
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
    private readonly IRepository<CompetitorModel> _repository;
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
    public CompetitorsPageViewModel(IPopupService popupService, ILogger<CompetitorsPageViewModel> logger, ResourceProvider resourceProvider, IRepository<CompetitorModel> repository)
        : base(logger, resourceProvider, popupService)
    {
        _repository = repository;
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
    private async Task Add()
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
            var competitor = result.Result;
            // Add category link using a stub to avoid UNIQUE constraint on Category.Id
            competitor.Categories = new List<CategoryModel> { new CategoryModel { Id = Category.Id } };

            await _repository.AddAsync(competitor);
            
            // Link back the full category for UI/Model consistency in memory
            competitor.Categories = new List<CategoryModel> { Category };
            Competitors.Add(competitor);
        }
    }

    [RelayCommand]
    private async Task Edit(CompetitorModel competitor)
    {
        if (Category == null) return;

        var queryAttributes = new Dictionary<string, object>
        {
            [nameof(CompetitorPopupViewModel.Competitor)] = competitor,
            [nameof(CompetitorPopupViewModel.Category)] = Category,
        } as IDictionary<string, object>;

        var result = await _popupService.ShowPopupAsync<CompetitorPopupViewModel, CompetitorModel?>(Shell.Current, shellParameters: queryAttributes);

        if (result != null && result.Result != null)
        {
            var updated = result.Result;
            
            // Map properties to the original instance to maintain object identity in the UI
            competitor.FirstName = updated.FirstName;
            competitor.LastName = updated.LastName;
            competitor.Genre = updated.Genre;
            competitor.BirthDate = updated.BirthDate;
            competitor.Club = updated.Club;
            competitor.Weight = updated.Weight;

            // Use stubs for categories during update to avoid circularity tracking conflicts
            var categoryStubs = competitor.Categories?.Select(c => new CategoryModel { Id = c.Id }).ToList();
            
            // Create a temporary clone for database update to not mess with the UI's full objects
            var dbCompetitor = new CompetitorModel
            {
                Id = competitor.Id,
                FirstName = competitor.FirstName,
                LastName = competitor.LastName,
                Genre = competitor.Genre,
                BirthDate = competitor.BirthDate,
                Club = competitor.Club,
                Weight = competitor.Weight,
                Categories = categoryStubs,
                Warnings = null // Do not update warnings through this path
            };

            await _repository.UpdateAsync(dbCompetitor);

            // UI Refresh
            var index = Competitors.IndexOf(competitor);
            if (index != -1)
            {
                Competitors[index] = null!;
                Competitors[index] = competitor;
            }
        }
    }

    [RelayCommand]
    private async Task Delete(CompetitorModel competitor)
    {
        if (competitor == null) return;

        bool confirm = await DisplayConfirmation(
            Resources.CONFIRM_DELETE,
            Resources.DELETE_COMPETITOR_MESSAGE,
            Resources.YES,
            Resources.NO);

        if (confirm)
        {
            await _repository.DeleteAsync(competitor);
            Competitors.Remove(competitor);
        }
    }
    #endregion
}
