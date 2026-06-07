#region Imports
using House.Of.Arbitration.ViewModels.Core;
#endregion

namespace House.Of.Arbitration.Views.Core;

/// <summary>
/// Non-generic base class for type checking and common lifecycle methods.
/// Includes a built-in loading indicator (ActivityIndicator) via a ControlTemplate.
/// </summary>
public abstract class BaseView : ContentView
{
    #region Bindable Properties
    public static readonly BindableProperty IsBusyProperty = BindableProperty.Create(
        nameof(IsBusy),
        typeof(bool),
        typeof(BaseView),
        false);

    /// <summary>
    /// Gets or sets a value indicating whether the view is currently busy (loading).
    /// </summary>
    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }
    #endregion

    #region Constructor
    protected BaseView()
    {
        // Define a global ControlTemplate for all inheriting views
        // This template wraps the content in a Grid with an ActivityIndicator overlay
        ControlTemplate = new ControlTemplate(() =>
        {
            var grid = new Grid();

            // The main content of the view
            var presenter = new ContentPresenter();
            grid.Children.Add(presenter);

            // Loading Overlay
            var overlay = new Grid
            {
                BackgroundColor = Color.FromRgba(0, 0, 0, 0.1),
                IsVisible = false, // Controlled by trigger
                InputTransparent = false
            };

            var indicator = new ActivityIndicator
            {
                IsRunning = false, // Controlled by trigger
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                //Color = (Color?)Application.Current?.Resources["Blue"] ?? Colors.White
                Color = Colors.Black ?? Colors.White
            };

            overlay.Children.Add(indicator);
            grid.Children.Add(overlay);

            // Add triggers to show/hide based on IsBusy property
            var busyTrigger = new DataTrigger(typeof(Grid))
            {
                Binding = new Binding(nameof(IsBusy), source: RelativeBindingSource.TemplatedParent),
                Value = true
            };
            busyTrigger.Setters.Add(new Setter { Property = VisualElement.IsVisibleProperty, Value = true });
            overlay.Triggers.Add(busyTrigger);

            var indicatorTrigger = new DataTrigger(typeof(ActivityIndicator))
            {
                Binding = new Binding(nameof(IsBusy), source: RelativeBindingSource.TemplatedParent),
                Value = true
            };
            indicatorTrigger.Setters.Add(new Setter { Property = ActivityIndicator.IsRunningProperty, Value = true });
            indicator.Triggers.Add(indicatorTrigger);

            return grid;
        });
    }
    #endregion

    #region Lifecycle Methods
    public abstract Task OnNavigatedTo();
    public abstract Task OnNavigatedFrom();
    public abstract Task OnAppearing();
    public abstract Task OnDisappearing();
    #endregion
}

/// <summary>
/// Base class for all ContentViews used within the MasterPage.
/// Automatically binds its IsBusy property to the ViewModel's IsBusy property.
/// </summary>
/// <typeparam name="TViewModel">The type of the ViewModel.</typeparam>
public abstract class BaseView<TViewModel> : BaseView where TViewModel : BaseViewModel
{
    public TViewModel ViewModel => (TViewModel)BindingContext;

    protected BaseView(TViewModel viewModel)
    {
        BindingContext = viewModel;

        // Auto-bind View.IsBusy to ViewModel.IsBusy
        this.SetBinding(BaseView.IsBusyProperty, new Binding(nameof(BaseViewModel.IsBusy)));
    }

    public override Task OnNavigatedTo() => Task.CompletedTask;

    public override Task OnNavigatedFrom() => Task.CompletedTask;

    public override Task OnAppearing() => ViewModel?.OnAppearing() ?? Task.CompletedTask;

    public override Task OnDisappearing() => ViewModel?.OnDisappearing() ?? Task.CompletedTask;
}
