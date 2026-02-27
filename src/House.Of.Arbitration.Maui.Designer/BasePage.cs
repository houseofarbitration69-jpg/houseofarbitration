using Microsoft.Maui.Controls;
using House.Of.Arbitration.Maui.Designer.Models;
using House.Of.Arbitration.Maui.Designer.Helpers;
using System.Collections.Generic;

namespace House.Of.Arbitration.Maui.Designer;

public class BasePage : ContentPage
{
    public static readonly BindableProperty IsMenuVisibleProperty =
        BindableProperty.Create(nameof(IsMenuVisible), typeof(bool), typeof(BasePage), true);

    public static readonly BindableProperty IsBackButtonVisibleProperty =
        BindableProperty.Create(nameof(IsBackButtonVisible), typeof(bool), typeof(BasePage), true);

    public static readonly BindableProperty MenuItemsProperty =
        BindableProperty.Create(nameof(MenuItems), typeof(List<MenuNavItem>), typeof(BasePage), null);

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

    public List<MenuNavItem> MenuItems
    {
        get => (List<MenuNavItem>)GetValue(MenuItemsProperty);
        set => SetValue(MenuItemsProperty, value);
    }

    public Command BackCommand { get; }

    public BasePage()
    {
        BackCommand = new Command(async () => await OnBackButtonClicked());
        
        // Default items with FontAwesome icons
        MenuItems = new List<MenuNavItem>
        {
            new MenuNavItem { Title = "Home", Icon = IconFont.House, Route = "MainPage" },
            new MenuNavItem { Title = "Search", Icon = IconFont.MagnifyingGlass, Route = "SearchPage" },
            new MenuNavItem { Title = "Add", Icon = IconFont.Plus, Route = "AddPage" },
            new MenuNavItem { Title = "Notifs", Icon = IconFont.Bell, Route = "NotificationsPage" },
            new MenuNavItem { Title = "Settings", Icon = IconFont.User, Route = "WizardPage" }
        };

        // Apply the template defined in App.xaml
        //ControlTemplate = (ControlTemplate)Application.Current.Resources["MasterPageTemplate"];
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