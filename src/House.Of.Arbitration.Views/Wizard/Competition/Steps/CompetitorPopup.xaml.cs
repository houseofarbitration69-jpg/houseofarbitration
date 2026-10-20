#region Imports
using House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;
using House.Of.Arbitration.Views.Core.Helpers;
#endregion

namespace House.Of.Arbitration.Views.Wizard.Competition.Steps;

public partial class CompetitorPopup
{
    #region Constructors
	public CompetitorPopup(CompetitorPopupViewModel viewModel)
	{
		InitializeComponent();
        BindingContext = viewModel;
        this.SetNavigationBarColor();

        // Calcul dynamique de la hauteur max selon l'écran disponible (par ex. 55% de l'écran pour laisser place au clavier)
        var displayInfo = DeviceDisplay.Current.MainDisplayInfo;
        if (displayInfo.Height > 0 && displayInfo.Density > 0)
        {
            double screenHeightDp = displayInfo.Height / displayInfo.Density;
            PopupBorder.MaximumHeightRequest = Math.Min(400, screenHeightDp * 0.52);
        }
        else
        {
            PopupBorder.MaximumHeightRequest = 380;
        }
	}
    #endregion
}
