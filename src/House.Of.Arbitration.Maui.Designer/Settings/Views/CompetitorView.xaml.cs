using CommunityToolkit.Maui.Views;
using House.Of.Arbitration.Maui.Designer.Models;

namespace House.Of.Arbitration.Maui.Designer.Settings.Views;

public partial class CompetitorView : Popup<CompetitorModel>
{
	public CompetitorView(CompetitorViewModel vm)
	{
		InitializeComponent();
		this.BindingContext = vm;
	}
}