using CommunityToolkit.Mvvm.Input;

namespace House.Of.Arbitration.Maui.Designer;

public partial class HomePage : BasePage
{
    public HomePage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    [RelayCommand]
    private async Task NavigateTo(string name)
    {
        await AppShell.Current.GoToAsync(name);
    }
}
