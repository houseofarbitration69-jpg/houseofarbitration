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
    private readonly IRepository<CompetitorCategoryModel> _competitorCategoryRepository;
    private readonly IRepository<DrawModel> _drawsRepository;
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
                // In many-to-many with join table, value.Competitors is List<CompetitorCategoryModel>
                var competitorModels = value.Competitors?.Select(cc => cc.Competitor).ToList() ?? new();
                Competitors = new ObservableCollection<CompetitorModel>(competitorModels);
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
    public CompetitorsPageViewModel(
        IPopupService popupService, 
        ILogger<CompetitorsPageViewModel> logger, 
        ResourceProvider resourceProvider, 
        IRepository<CompetitorModel> repository,
        IRepository<CompetitorCategoryModel> competitorCategoryRepository,
        IRepository<DrawModel> drawsRepository)
        : base(logger, resourceProvider, popupService)
    {
        _repository = repository;
        _competitorCategoryRepository = competitorCategoryRepository;
        _drawsRepository = drawsRepository;
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
    private async Task DeleteDrawAsync()
    {
        if (Category == null) return;
        var draws = await _drawsRepository.GetAllAsync();
        var existingDraw = draws?.FirstOrDefault(d => d.CategoryId == Category.Id);
        if (existingDraw != null)
        {
            await _drawsRepository.DeleteAsync(existingDraw);
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

            // 1. Save or Update the Competitor
            await _repository.AddAsync(competitor);
            
            // 2. Create the link in join table
            var link = new CompetitorCategoryModel
            {
                CompetitorId = competitor.Id,
                Competitor = competitor,
                CategoryId = Category.Id,
                Category = Category
            };
            await _competitorCategoryRepository.AddAsync(link);

            // 3. Update local collections
            if (Category.Competitors == null) Category.Competitors = new();
            Category.Competitors.Add(link);
            Competitors.Add(competitor);

            // 4. Invalidate Draw
            await DeleteDrawAsync();
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
            
            // Map properties to the original instance to maintain object identity
            competitor.FirstName = updated.FirstName;
            competitor.LastName = updated.LastName;
            competitor.Genre = updated.Genre;
            competitor.BirthDate = updated.BirthDate;
            competitor.Club = updated.Club;
            competitor.Weight = updated.Weight;

            // Create flat clone for DB update to avoid tracking conflicts
            var dbCompetitor = new CompetitorModel
            {
                Id = competitor.Id,
                FirstName = competitor.FirstName,
                LastName = competitor.LastName,
                Genre = competitor.Genre,
                BirthDate = competitor.BirthDate,
                Club = competitor.Club,
                Weight = competitor.Weight
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
        if (competitor == null || Category == null) return;

        bool confirm = await DisplayConfirmation(
            Resources.CONFIRM_DELETE,
            Resources.DELETE_COMPETITOR_MESSAGE,
            Resources.YES,
            Resources.NO);

        if (confirm)
        {
            // 1. Find the link in the join table
            var links = await _competitorCategoryRepository.GetAllAsync();
            var linkToRemove = links?.FirstOrDefault(cc => cc.CompetitorId == competitor.Id && cc.CategoryId == Category.Id);
            
            if (linkToRemove != null)
            {
                await _competitorCategoryRepository.DeleteAsync(linkToRemove);
                Category.Competitors.Remove(linkToRemove);
            }

            // Note: We don't delete the competitor itself from the database 
            // because they might be registered in other categories.
            // We only remove them from THIS category.
            Competitors.Remove(competitor);

            // 2. Invalidate Draw
            await DeleteDrawAsync();
        }
    }
    #endregion
}
