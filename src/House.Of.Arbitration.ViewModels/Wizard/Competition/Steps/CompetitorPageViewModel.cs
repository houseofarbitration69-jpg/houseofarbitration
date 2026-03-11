#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
using House.Of.Arbitration.Localization;
using Microsoft.Extensions.Logging;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

[QueryProperty(nameof(Competitor), "Competitor")]
public partial class CompetitorPageViewModel : BaseViewModel
{
    #region Attributs
    private CompetitorModel _competitor = new();
    private string _pageTitle = "Nouveau Compétiteur";
    #endregion

    #region Properties
    public CompetitorModel Competitor
    {
        get => _competitor;
        set
        {
            if(_competitor != value)
            {
                OnCompetitorChanged(value);
            }

            SetProperty(ref _competitor, value);
        }
    }

    public string PageTitle
    {
        get => _pageTitle;
        set => SetProperty(ref _pageTitle, value);
    }
    #endregion

    #region Constructor
    public CompetitorPageViewModel(ILogger<CompetitorPageViewModel> logger, ResourceProvider resourceProvider) 
        : base(logger, resourceProvider)
    {
    }
    #endregion

    #region Private Methods
    private void OnCompetitorChanged(CompetitorModel value)
    {
        if (value != null)
        {
            PageTitle = string.IsNullOrWhiteSpace(value.FirstName) ? "Nouveau Compétiteur" : $"Modifier {value.FirstName} {value.LastName}";
        }
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task Save()
    {
        // On retourne à la page précédente en passant le compétiteur modifié (optionnel si c'est la même instance)
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task Cancel()
    {
        // En cas d'annulation, on pourrait avoir besoin de logique spécifique, 
        // mais ici un simple retour suffit.
        await Shell.Current.GoToAsync("..");
    }
    #endregion
}
