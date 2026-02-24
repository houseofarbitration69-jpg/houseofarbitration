using Microsoft.Maui.Controls;

namespace House.Of.Arbitration.Maui.Designer;

public class BasePage : ContentPage
{
    public static readonly BindableProperty IsMenuVisibleProperty =
        BindableProperty.Create(nameof(IsMenuVisible), typeof(bool), typeof(BasePage), true);

    public static readonly BindableProperty IsBackButtonVisibleProperty =
        BindableProperty.Create(nameof(IsBackButtonVisible), typeof(bool), typeof(BasePage), true);

    public bool IsMenuVisible
    {
        get => (bool)GetValue(IsMenuVisibleProperty);
        set => SetValue(IsMenuVisibleProperty, value);
    }

    public bool IsBackButtonVisible
    {
        get => (bool)GetValue(IsBackButtonVisibleProperty);
        set => SetValue(IsBackButtonVisibleProperty, value);
    }

    public Command BackCommand { get; }

    public BasePage()
    {
        BackCommand = new Command(async () => await OnBackButtonClicked());
        // Apply the template defined in App.xaml
        ControlTemplate = (ControlTemplate)Application.Current.Resources["MasterPageTemplate"];
    }

    private async Task OnBackButtonClicked()
    {
        if (Shell.Current.Navigation.NavigationStack.Count > 1)
        {
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            // If we are on a tab, go back to the first tab (Home)
            var location = Shell.Current.CurrentState.Location.OriginalString;
            if (!location.Contains("MainPage"))
            {
                await Shell.Current.GoToAsync("///MainPage");
            }
        }
    }
}