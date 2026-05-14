#region Imports
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
#endregion

namespace House.Of.Arbitration.ViewModels;

public partial class RefereeDataViewModel : BaseViewModel
{
    #region Services
    private readonly IRepository<RefereeDataModel> _refereeDataRepository;
    private readonly IRepository<CompetitionModel> _competitionRepository;
    private readonly IRepository<CategoryModel> _categoryRepository;
    #endregion

    #region Attributs
    private ObservableCollection<RefereeDataModel> _refereeDatas = new();
    private ObservableCollection<CompetitionModel> _competitions = new();
    private ObservableCollection<CategoryModel> _categories = new();
    private CompetitionModel? _selectedCompetition;
    private CategoryModel? _selectedCategory;
    private List<RefereeDataModel> _allDatas = new();
    private List<CategoryModel> _allCategories = new();
    #endregion

    #region Properties
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

    public CompetitionModel? SelectedCompetition
    {
        get => _selectedCompetition;
        set
        {
            if (SetProperty(ref _selectedCompetition, value))
            {
                UpdateCategories();
                SelectedCategory = null;
                FilterDatas();
            }
        }
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
    #endregion

    #region Constructors
    public RefereeDataViewModel(
        ILogger<RefereeDataViewModel> logger,
        ResourceProvider resourceProvider,
        CommunityToolkit.Maui.IPopupService popupService,
        IRepository<RefereeDataModel> refereeDataRepository,
        IRepository<CompetitionModel> competitionRepository,
        IRepository<CategoryModel> categoryRepository
    ) : base(logger, resourceProvider, popupService)
    {
        _refereeDataRepository = refereeDataRepository;
        _competitionRepository = competitionRepository;
        _categoryRepository = categoryRepository;
    }
    #endregion

    #region Override Methods
    public override async Task OnAppearing()
    {
        var comps = await _competitionRepository.GetAllAsync();
        if (comps != null)
        {
            Competitions = new ObservableCollection<CompetitionModel>(comps);
        }

        var cats = await _categoryRepository.GetAllAsync();
        if (cats != null)
        {
            _allCategories = cats.ToList();
            UpdateCategories();
        }

        var datas = await _refereeDataRepository.GetAllAsync(
            "DrawKnockoutModel.Draw.Category", 
            "DrawOrder.Draw.Category", 
            "DrawPools.Draw.Category", 
            "Competitor");
        
        if (datas != null)
        {
            _allDatas = datas.ToList();
            FilterDatas();
        }
    }
    #endregion

    #region Private Methods
    [RelayCommand]
    private void ResetFilter()
    {
        SelectedCompetition = null;
        SelectedCategory = null;
    }

    private void UpdateCategories()
    {
        if (SelectedCompetition == null)
        {
            Categories = new ObservableCollection<CategoryModel>(_allCategories);
        }
        else
        {
            Categories = new ObservableCollection<CategoryModel>(_allCategories.Where(c => c.CompetitionId == SelectedCompetition.Id));
        }
    }

    private void FilterDatas()
    {
        var filtered = _allDatas.AsEnumerable();

        if (SelectedCompetition != null)
        {
            filtered = filtered.Where(d => 
                (d.DrawKnockoutModel?.Draw?.Category?.CompetitionId == SelectedCompetition.Id) ||
                (d.DrawOrder?.Draw?.Category?.CompetitionId == SelectedCompetition.Id) ||
                (d.DrawPools?.Draw?.Category?.CompetitionId == SelectedCompetition.Id)
            );
        }

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
    #endregion
}
