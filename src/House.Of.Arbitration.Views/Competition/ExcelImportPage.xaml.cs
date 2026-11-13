#region Imports
using House.Of.Arbitration.ViewModels.Competition;
using House.Of.Arbitration.Views.Core;
#endregion

namespace House.Of.Arbitration.Views.Competition;

/// <summary>
/// Page d'import/export Excel pour les compétitions
/// </summary>
public partial class ExcelImportPage : BasePage<ExcelImportViewModel>
{
    public ExcelImportPage(ExcelImportViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}
