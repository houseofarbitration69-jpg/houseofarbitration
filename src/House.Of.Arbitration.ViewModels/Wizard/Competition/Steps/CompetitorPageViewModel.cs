using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
using House.Of.Arbitration.Localization;
using Microsoft.Extensions.Logging;

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

[QueryProperty(nameof(Competitor), "Competitor")]
public partial class CompetitorPageViewModel : BaseViewModel
{
    [ObservableProperty]
    private CompetitorModel _competitor = new();

    [ObservableProperty]
    private string _pageTitle = "Nouveau Compétiteur";

    public CompetitorPageViewModel(ILogger<CompetitorPageViewModel> logger, ResourceProvider resourceProvider) 
        : base(logger, resourceProvider)
    {
    }

    partial void OnCompetitorChanged(CompetitorModel value)
    {
        if (value != null)
        {
            PageTitle = string.IsNullOrWhiteSpace(value.FirstName) ? "Nouveau Compétiteur" : $"Modifier {value.FirstName} {value.LastName}";
        }
    }

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
}
