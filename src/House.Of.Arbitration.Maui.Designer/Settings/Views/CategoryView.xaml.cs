using CommunityToolkit.Maui.Views;
using House.Of.Arbitration.Maui.Designer.Models;

namespace House.Of.Arbitration.Maui.Designer.Settings.Views;

public partial class CategoryView : Popup<CategoryModel>
{
	public CategoryView(CategoryViewModel vm)
	{
		InitializeComponent();
		this.BindingContext = vm;
	}
}