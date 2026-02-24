using Microsoft.Maui.Controls;
using System.Linq;
using System.Threading.Tasks;

namespace House.Of.Arbitration.Maui.Designer.Controls;

public partial class BottomMenuView : ContentView
{
    private static int _lastSelectedIndex = 0;
    private int _currentIndex = 0;
    private static readonly string[] Routes = { "MainPage", "SearchPage", "AddPage", "NotificationsPage", "ProfilePage" };

    public BottomMenuView()
    {
        InitializeComponent();
        this.Loaded += OnBottomMenuViewLoaded;
    }

    private void OnBottomMenuViewLoaded(object sender, EventArgs e)
    {
        UpdateInitialPosition();
    }

    private async void UpdateInitialPosition()
    {
        if (Shell.Current?.CurrentState?.Location == null) return;

        var location = Shell.Current.CurrentState.Location.OriginalString;
        var currentRoute = location.Split('/').LastOrDefault(s => !string.IsNullOrWhiteSpace(s));
        
        int targetIndex = Array.IndexOf(Routes, currentRoute);
        if (targetIndex < 0) targetIndex = 0; // Default to Home

        if (targetIndex != _lastSelectedIndex)
        {
            // Start from the previous position to create a transition effect
            Grid.SetColumn(SelectionIndicator, _lastSelectedIndex);
            _currentIndex = _lastSelectedIndex;

            // Wait a tiny bit for the layout to be ready
            await Task.Delay(50);

            double itemWidth = MenuGrid.Width / 5;
            double targetTranslation = (targetIndex - _currentIndex) * itemWidth;

            await SelectionIndicator.TranslateTo(targetTranslation, 0, 300, Easing.CubicInOut);
            
            SelectionIndicator.TranslationX = 0;
            Grid.SetColumn(SelectionIndicator, targetIndex);
            _currentIndex = targetIndex;
            _lastSelectedIndex = targetIndex;
        }
        else
        {
            _currentIndex = targetIndex;
            Grid.SetColumn(SelectionIndicator, targetIndex);
            _lastSelectedIndex = targetIndex;
        }
    }

    private async void OnMenuItemTapped(object sender, EventArgs e)
    {
        if (sender is View view && view.GestureRecognizers[0] is TapGestureRecognizer tap && tap.CommandParameter is string indexStr)
        {
            int index = int.Parse(indexStr);
            if (index == _currentIndex) return;

            string route = Routes[index];
            _lastSelectedIndex = _currentIndex; // Save current as last before navigating

            // Navigate
            await Shell.Current.GoToAsync($"///{route}");
            
            // Note: The animation for the "new" page will be handled by its own Loaded event
        }
    }

    public event EventHandler<int> OnItemSelected;
}