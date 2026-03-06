#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
#endregion

namespace House.Of.Arbitration.ViewModels;

public partial class HomeViewModel : BaseViewModel
{
    #region Services
    private readonly IRepository<CompetitionModel> _repository;
    #endregion

    #region Attributs
    private string _title = String.Empty;
    private ObservableCollection<CompetitionModel>? _competitions;
    private ObservableCollection<string>? _maListeDeChaines;
    #endregion

    #region Properties
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
    
    public ObservableCollection<CompetitionModel>? Competitions
    {
        get => _competitions;
        set => SetProperty(ref _competitions, value);
    }

    public ObservableCollection<String>? MaListeDeChaines
    {
        get => _maListeDeChaines;
        set => SetProperty(ref _maListeDeChaines, value);
    }
    #endregion

    #region Constructors
    /// <summary>
    /// 
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="resourceProvider"></param>
    /// <param name="repository"></param>
    public HomeViewModel(ILogger<HomeViewModel> logger, ResourceProvider resourceProvider, IRepository<CompetitionModel> repository) : base(logger, resourceProvider)
    {
        Title = resourceProvider.APPLICATION_NAME;

        _repository = repository;
    }
    #endregion

    public override async Task OnAppearing()
    {
        var data = await _repository.GetAllAsync(c => c.Categories);
        Competitions = new ObservableCollection<CompetitionModel>(data ?? new List<CompetitionModel>());

        MaListeDeChaines = new ObservableCollection<string>()
        {
            "A",
            "B",
            "C",
        };

        await base.OnAppearing();
    }

    //[RelayCommand(CanExecute = nameof(StartCompetitionIsEnabled))]
    //private async Task ShowCompetitions()
    //{

    //}
}
