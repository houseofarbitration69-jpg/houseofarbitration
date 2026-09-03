using System.Collections;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace House.Of.Arbitration.Controls;

public partial class SuggestEntry : ContentView
{
    public SuggestEntry()
    {
        InitializeComponent();
        SetValue(SuggestionsPropertyKey, new ObservableCollection<object>());
    }

    #region Bindable Properties

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(SuggestEntry), string.Empty, BindingMode.TwoWay);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder), typeof(string), typeof(SuggestEntry), string.Empty);

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public static readonly BindableProperty PlaceholderColorProperty = BindableProperty.Create(
        nameof(PlaceholderColor), typeof(Color), typeof(SuggestEntry), Colors.Gray);

    public Color PlaceholderColor
    {
        get => (Color)GetValue(PlaceholderColorProperty);
        set => SetValue(PlaceholderColorProperty, value);
    }

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor), typeof(Color), typeof(SuggestEntry), Colors.Black);

    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public static readonly BindableProperty StrokeProperty = BindableProperty.Create(
        nameof(Stroke), typeof(Brush), typeof(SuggestEntry), Brush.Gray);

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public static readonly BindableProperty StrokeThicknessProperty = BindableProperty.Create(
        nameof(StrokeThickness), typeof(double), typeof(SuggestEntry), 1.0);

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius), typeof(CornerRadius), typeof(SuggestEntry), new CornerRadius(5));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IEnumerable), typeof(SuggestEntry), null);

    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly BindableProperty AllowNewValuesProperty = BindableProperty.Create(
        nameof(AllowNewValues), typeof(bool), typeof(SuggestEntry), false);

    public bool AllowNewValues
    {
        get => (bool)GetValue(AllowNewValuesProperty);
        set => SetValue(AllowNewValuesProperty, value);
    }

    private static readonly BindablePropertyKey SuggestionsPropertyKey = BindableProperty.CreateReadOnly(
        nameof(Suggestions), typeof(ObservableCollection<object>), typeof(SuggestEntry), null);

    public static readonly BindableProperty SuggestionsProperty = SuggestionsPropertyKey.BindableProperty;

    public ObservableCollection<object> Suggestions
    {
        get => (ObservableCollection<object>)GetValue(SuggestionsProperty);
        private set => SetValue(SuggestionsPropertyKey, value);
    }

    private static readonly BindablePropertyKey IsSuggestionsVisiblePropertyKey = BindableProperty.CreateReadOnly(
        nameof(IsSuggestionsVisible), typeof(bool), typeof(SuggestEntry), false);

    public static readonly BindableProperty IsSuggestionsVisibleProperty = IsSuggestionsVisiblePropertyKey.BindableProperty;

    public bool IsSuggestionsVisible
    {
        get => (bool)GetValue(IsSuggestionsVisibleProperty);
        private set => SetValue(IsSuggestionsVisiblePropertyKey, value);
    }

    #endregion

    #region Event Handlers

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        FilterSuggestions(e.NewTextValue);
    }

    private async void OnEntryFocused(object sender, FocusEventArgs e)
    {
        FilterSuggestions(Text);
        await ScrollIntoViewAsync();
    }

    private async void OnEntryUnfocused(object sender, FocusEventArgs e)
    {
        // Delay hiding to allow selection to register if user clicked the list
        await Task.Delay(200);
        IsSuggestionsVisible = false;
        ValidateAndAddValue();
    }

    private void OnEntryCompleted(object sender, EventArgs e)
    {
        ValidateAndAddValue();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is var selectedItem && selectedItem != null)
        {
            Text = selectedItem.ToString()!;
            IsSuggestionsVisible = false;
            
            if (sender is CollectionView cv)
            {
                cv.SelectedItem = null;
            }

            EntryField?.Unfocus();
        }
    }

    #endregion

    private void ValidateAndAddValue()
    {
        if (!AllowNewValues || string.IsNullOrWhiteSpace(Text) || ItemsSource == null)
            return;

        var currentText = Text.Trim();
        var lowerText = currentText.ToLowerInvariant();
        bool exists = false;

        foreach (var item in ItemsSource)
        {
            if (item?.ToString()?.ToLowerInvariant() == lowerText)
            {
                exists = true;
                break;
            }
        }

        if (!exists && ItemsSource is System.Collections.IList list)
        {
            list.Add(currentText);
        }
    }

    private async void FilterSuggestions(string? searchText)
    {
        // Ensure we don't show suggestions if the entry is not focused (e.g. programmatic text change)
        if (EntryField == null || !EntryField.IsFocused)
        {
            IsSuggestionsVisible = false;
            return;
        }

        if (ItemsSource == null)
        {
            IsSuggestionsVisible = false;
            return;
        }

        var lowerText = (searchText ?? string.Empty).Trim().ToLowerInvariant();
        var filtered = new List<object>();

        foreach (var item in ItemsSource)
        {
            if (item == null) continue;
            var itemString = item.ToString();
            if (string.IsNullOrEmpty(itemString)) continue;

            if (string.IsNullOrEmpty(lowerText) || itemString.ToLowerInvariant().Contains(lowerText))
            {
                filtered.Add(item);
            }
        }

        Suggestions.Clear();
        foreach (var item in filtered)
        {
            Suggestions.Add(item);
        }

        IsSuggestionsVisible = Suggestions.Count > 0;

        if (IsSuggestionsVisible)
        {
            await ScrollIntoViewAsync();
        }
    }

    private async Task ScrollIntoViewAsync()
    {
        try
        {
            Element? current = this.Parent;
            while (current != null && !(current is ScrollView))
            {
                current = current.Parent;
            }

            if (current is ScrollView scrollView)
            {
                await Task.Delay(100);
                await scrollView.ScrollToAsync(this, ScrollToPosition.Start, true);
            }
        }
        catch
        {
            // Ignore if scroll animation fails
        }
    }
}
