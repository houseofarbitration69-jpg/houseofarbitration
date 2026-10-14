#region Imports
using House.Of.Arbitration.ViewModels;
using House.Of.Arbitration.Views.Core;
#endregion

namespace House.Of.Arbitration.Views;

public partial class TaoluCodesPage : BasePage<TaoluCodesViewModel>
{
    public TaoluCodesPage(TaoluCodesViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}
