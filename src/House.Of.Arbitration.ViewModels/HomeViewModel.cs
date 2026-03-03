#region Imports
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
#endregion

namespace House.Of.Arbitration.ViewModels;

public partial class HomeViewModel : BaseViewModel
{
    #region Services
    private readonly IRepository<CompetitionModel> _repository;
    #endregion

    #region Attributs
    private string _title = String.Empty;
    private bool _startCompetitionEnabled = false;
    #endregion

    #region Properties
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public bool StartCompetitionIsEnabled
    {
        get => _startCompetitionEnabled;
        set => SetProperty(ref _startCompetitionEnabled, value);
    }
    #endregion

    public HomeViewModel(ILogger<HomeViewModel> logger, ResourceProvider resourceProvider, IRepository<CompetitionModel> repository) : base(logger, resourceProvider)
    {
        Title = resourceProvider.APPLICATION_NAME;

        _repository = repository;
    }

    public override async Task OnAppearing()
    {
        StartCompetitionIsEnabled = (await _repository.GetAllAsync())?.Count > 0;
        await base.OnAppearing();
    }

    [RelayCommand(CanExecute = nameof(StartCompetitionIsEnabled))]
    private async Task ShowCompetitions()
    {

    }
}
