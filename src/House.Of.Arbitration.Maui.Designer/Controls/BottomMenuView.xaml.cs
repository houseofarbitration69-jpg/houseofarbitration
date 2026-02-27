using Microsoft.Maui.Controls;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using House.Of.Arbitration.Maui.Designer.Models;
using System.Collections;

namespace House.Of.Arbitration.Maui.Designer.Controls;

public partial class BottomMenuView : ContentView
{
    private static int _lastSelectedIndex = 0;
    private int _currentIndex = 0;

    public static readonly BindableProperty MenuItemsProperty =
        BindableProperty.Create(nameof(MenuItems), typeof(IEnumerable), typeof(BottomMenuView), null, propertyChanged: OnMenuItemsChanged);

    public IEnumerable MenuItems
    {
        get => (IEnumerable)GetValue(MenuItemsProperty);
        set => SetValue(MenuItemsProperty, value);
    }

    public BottomMenuView()
    {
        InitializeComponent();
        this.Loaded += OnBottomMenuViewLoaded;
    }

    private static void OnMenuItemsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is BottomMenuView menu && newValue is IEnumerable items)
        {
            var itemList = items.Cast<object>().ToList();
            menu.MenuGrid.ColumnDefinitions.Clear();
            foreach (var _ in itemList)
            {
                menu.MenuGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }

            // Set Grid.Column for each child manually since BindableLayout doesn't do it for Grid automatically
            menu.UpdateGridColumns();
        }
    }

    private void UpdateGridColumns()
    {
        for (int i = 0; i < MenuGrid.Children.Count; i++)
        {
            Grid.SetColumn((BindableObject)MenuGrid.Children[i], i);
        }
    }

    private void OnBottomMenuViewLoaded(object sender, EventArgs e)
    {
        this.Loaded -= OnBottomMenuViewLoaded;
        UpdateGridColumns();
        UpdateInitialPosition();
    }

    private async void UpdateInitialPosition()
    {
        if (Shell.Current?.CurrentState?.Location == null || MenuItems == null) return;

        var items = MenuItems.Cast<MenuNavItem>().ToList();
        var location = Shell.Current.CurrentState.Location.OriginalString;
        var currentRoute = location.Split('/').LastOrDefault(s => !string.IsNullOrWhiteSpace(s));
        
        int targetIndex = items.FindIndex(m => m.Route == currentRoute);
        if (targetIndex < 0) targetIndex = 0;

        // Set indicator width based on grid columns
        SelectionIndicator.WidthRequest = (ContainerGrid.Width / items.Count) - 30;

        if (targetIndex != _lastSelectedIndex)
        {
            _currentIndex = _lastSelectedIndex;
            UpdateIndicatorPosition(_currentIndex);

            await Task.Delay(100);

            double itemWidth = ContainerGrid.Width / items.Count;
            double targetTranslation = (targetIndex - _currentIndex) * itemWidth;

            await SelectionIndicator.TranslateTo(targetTranslation, 0, 300, Easing.CubicInOut);
            
            SelectionIndicator.TranslationX = 0;
            _currentIndex = targetIndex;
            _lastSelectedIndex = targetIndex;
        }
        
        UpdateIndicatorPosition(targetIndex);
        _lastSelectedIndex = targetIndex;
    }

    private void UpdateIndicatorPosition(int index)
    {
        if (MenuItems == null) return;
        var items = MenuItems.Cast<object>().ToList();
        if (items.Count == 0) return;

        double itemWidth = ContainerGrid.Width / items.Count;
        SelectionIndicator.TranslationX = 0;
        SelectionIndicator.Margin = new Thickness((itemWidth * index) + 15, 0, 15, 5);
    }

    private async void OnMenuItemTapped(object sender, EventArgs e)
    {
        if (sender is View view && view.GestureRecognizers[0] is TapGestureRecognizer tap && tap.CommandParameter is MenuNavItem item)
        {
            var items = MenuItems.Cast<MenuNavItem>().ToList();
            int index = items.IndexOf(item);
            if (index == _currentIndex || index < 0) return;

            _lastSelectedIndex = _currentIndex;
            await Shell.Current.GoToAsync($"///{item.Route}");
        }
    }
}