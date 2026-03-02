#region Imports
using House.Of.Arbitration.ViewModels.Core;
using System.Diagnostics;
#endregion

namespace House.Of.Arbitration.Views.Core;

/// <summary>
/// A generic base class for all pages in the application.
/// It automatically sets the BindingContext to the provided view model.
/// </summary>
/// <typeparam name="T">The type of the view model that this page is associated with.</typeparam>
public partial class BasePage<T> : ContentPage
    where T : BaseViewModel
{
    private static readonly ActivitySource ActivitySource = new ActivitySource("[REPLACE]");

    private readonly Stopwatch _pageLoadStopwatch;

    private Activity? _pageActivity;

    #region Constructors
    /// <summary>
    /// Initializes a new instance of the <see cref="BasePage{T}"/> class.
    /// </summary>
    /// <param name="viewModel">The view model instance to be used as the binding context.</param>
    public BasePage(T viewModel)
    {
        BindingContext = viewModel;
        _pageLoadStopwatch = new Stopwatch();
    }
    #endregion

    #region Override Methods
    /// <summary>
    /// Overrides the base method to call the corresponding OnAppearing method on the view model.
    /// </summary>
    protected override async void OnAppearing()
    {
        _pageLoadStopwatch.Start();

        base.OnAppearing();
        if (BindingContext is BaseViewModel vm)
        {
            await vm.OnAppearing();
        }

        _pageActivity = ActivitySource.StartActivity($"Page.{GetType().Name}");
        _pageActivity?.SetTag("page.name", GetType().Name);
        _pageActivity?.SetTag("page.event", "OnAppearing");
        _pageActivity?.AddEvent(new ActivityEvent("PageAppearing"));
    }

    /// <summary>
    /// Overrides the base method to call the corresponding OnDisappearing method on the view model.
    /// </summary>
    protected override async void OnDisappearing()
    {
        _pageLoadStopwatch.Stop();

        _pageActivity?.SetTag("page.duration_ms", _pageLoadStopwatch.ElapsedMilliseconds);
        _pageActivity?.AddEvent(new ActivityEvent("PageDisappearing"));
        _pageActivity?.SetStatus(ActivityStatusCode.Ok);
        _pageActivity?.Dispose();

        base.OnDisappearing();
        if (BindingContext is BaseViewModel vm)
        {
            await vm.OnDisappearing();
        }
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        _pageActivity?.AddEvent(new ActivityEvent("NavigatedFrom"));

        base.OnNavigatedFrom(args);
    }
    #endregion

    #region Protected Methods
    /// <summary>
    /// Tracks a user action using telemetry.
    /// </summary>
    protected void TrackUserAction(string action, Dictionary<string, object?>? properties = null)
    {
        using var activity = ActivitySource.StartActivity($"UserAction.{action}", ActivityKind.Internal);

        activity?.SetTag("action.name", action);
        activity?.SetTag("page.name", GetType().Name);

        if (properties != null)
        {
            foreach (var prop in properties)
            {
                activity?.SetTag($"action.{prop.Key}", prop.Value);
            }
        }
    }

    /// <summary>
    /// Handles the back button click logic.
    /// </summary>
    protected async Task OnBackButtonClicked()
    {
        if (Shell.Current.Navigation.NavigationStack.Count > 1)
        {
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            var location = Shell.Current.CurrentState.Location.OriginalString;
            if (!location.Contains("MainPage"))
            {
                await Shell.Current.GoToAsync("///MainPage");
            }
        }
    }
    #endregion
}
