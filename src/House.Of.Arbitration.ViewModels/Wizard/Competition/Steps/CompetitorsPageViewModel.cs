#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

public partial class CompetitorsPageViewModel : BaseViewModel, IQueryAttributable
{
    #region Services
    private readonly IRepository<CompetitorModel> _repository;
    private readonly IRepository<CompetitorCategoryModel> _competitorCategoryRepository;
    private readonly IRepository<DrawModel> _drawsRepository;
    private readonly IWarningService _warningService;
    #endregion

    #region Attributs
    private CategoryModel? _category;
    private ObservableCollection<CompetitorCategoryModel> _competitors = new();
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
                Competitors = new ObservableCollection<CompetitorCategoryModel>(value.Competitors ?? new());
            }
        }
    }

    public ObservableCollection<CompetitorCategoryModel> Competitors
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
        IRepository<DrawModel> drawsRepository,
        IWarningService warningService)
        : base(logger, resourceProvider, popupService)
    {
        _repository = repository;
        _competitorCategoryRepository = competitorCategoryRepository;
        _drawsRepository = drawsRepository;
        _warningService = warningService;
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

    #region Virtual Methods
    public override async Task OnAppearing()
    {
        if (Category != null)
        {
            // Reload the category with its registrations and warnings to ensure UI is up-to-date
            var links = await _competitorCategoryRepository.GetAllAsync("Competitor", "Warnings");
            var categoryLinks = links?.Where(l => l.CategoryId == Category.Id).ToList();

            if (categoryLinks != null)
            {
                Category.Competitors = categoryLinks;
                Competitors = new ObservableCollection<CompetitorCategoryModel>(categoryLinks);
            }
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

            // 1. Check if competitor already exists in DB (same FirstName, LastName and BirthDate)
            var allCompetitors = await _repository.GetAllAsync();
            var existingCompetitor = allCompetitors?.FirstOrDefault(c =>
                string.Equals(c.FirstName, competitor.FirstName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.LastName, competitor.LastName, StringComparison.OrdinalIgnoreCase) &&
                c.BirthDate.Date == competitor.BirthDate.Date);

            if (existingCompetitor != null)
            {
                // Use the existing competitor instead of creating a new one
                competitor = existingCompetitor;

                // Also update club/weight from popup if changed
                competitor.Club = result.Result.Club;
                competitor.Weight = (result.Result.Weight > 0) ? result.Result.Weight : existingCompetitor.Weight;
                await _repository.UpdateAsync(competitor);
            }
            else
            {
                // Save new competitor
                await _repository.AddAsync(competitor);
            }

            // 2. Create the link in join table if it doesn't already exist for this category
            var links = await _competitorCategoryRepository.GetAllAsync();
            var existingLink = links?.FirstOrDefault(cc => cc.CompetitorId == competitor.Id && cc.CategoryId == Category.Id);

            if (existingLink == null)
            {
                var link = new CompetitorCategoryModel
                {
                    CompetitorId = competitor.Id,
                    CategoryId = Category.Id,
                };

                await _competitorCategoryRepository.AddAsync(link);

                // 3. Update local collections
                if (Category.Competitors == null) Category.Competitors = new();
                Category.Competitors.Add(link);
                Competitors.Add(link);
            }

            // 4. Update Warnings
            await _warningService.UpdateWarningsForCompetitorAsync(competitor.Id);

            // 5. Invalidate Draw
            await DeleteDrawAsync();

            // 6. Refresh warnings from DB
            await OnAppearing();
        }
    }

    [RelayCommand]
    private async Task Edit(CompetitorCategoryModel link)
    {
        if (Category == null || link.Competitor == null) return;

        var queryAttributes = new Dictionary<string, object>
        {
            [nameof(CompetitorPopupViewModel.Competitor)] = link.Competitor,
            [nameof(CompetitorPopupViewModel.Category)] = Category,
        } as IDictionary<string, object>;

        var result = await _popupService.ShowPopupAsync<CompetitorPopupViewModel, CompetitorModel?>(Shell.Current, shellParameters: queryAttributes);

        if (result != null && result.Result != null)
        {
            var updated = result.Result;

            // Map properties to the original instance to maintain object identity
            link.Competitor.FirstName = updated.FirstName;
            link.Competitor.LastName = updated.LastName;
            link.Competitor.Genre = updated.Genre;
            link.Competitor.BirthDate = updated.BirthDate;
            link.Competitor.Club = updated.Club;
            link.Competitor.Weight = updated.Weight;

            // Create flat clone for DB update to avoid tracking conflicts
            var dbCompetitor = new CompetitorModel
            {
                Id = link.Competitor.Id,
                FirstName = link.Competitor.FirstName,
                LastName = link.Competitor.LastName,
                Genre = link.Competitor.Genre,
                BirthDate = link.Competitor.BirthDate,
                Club = link.Competitor.Club,
                Weight = link.Competitor.Weight
            };

            await _repository.UpdateAsync(dbCompetitor);

            // Update Warnings
            await _warningService.UpdateWarningsForCompetitorAsync(link.Competitor.Id);

            // Refresh warnings from DB
            await OnAppearing();
        }
    }

    [RelayCommand]
    private async Task Delete(CompetitorCategoryModel link)
    {
        if (link == null || Category == null) return;

        bool confirm = await DisplayConfirmation(
            Resources.CONFIRM_DELETE,
            Resources.DELETE_COMPETITOR_MESSAGE,
            Resources.YES,
            Resources.NO);

        if (confirm)
        {
            await _competitorCategoryRepository.DeleteAsync(link);
            Category.Competitors.Remove(link);
            Competitors.Remove(link);

            // Invalidate Draw
            await DeleteDrawAsync();
        }
    }
    #endregion
}
