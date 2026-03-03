#region Imports
using House.Of.Arbitration.Models.Helpers;
using System.Windows.Input;
#endregion

namespace House.Of.Arbitration.Controls;

public partial class IconButton : ContentView
{
    #region Bindable Properties
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(IconButton), string.Empty);

    /// <summary>
    /// Gets or sets the text for the button.
    /// </summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon), typeof(string), typeof(IconButton), string.Empty);

    /// <summary>
    /// Gets or sets the icon for the button.
    /// </summary>
    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly BindableProperty IconFontFamilyProperty = BindableProperty.Create(
        nameof(IconFontFamily), typeof(string), typeof(IconButton), FontHelper.FONTAWESOME_SOLID_NAME);

    /// <summary>
    /// Gets or sets the font family for the icon.
    /// </summary>
    public string IconFontFamily
    {
        get => (string)GetValue(IconFontFamilyProperty);
        set => SetValue(IconFontFamilyProperty, value);
    }

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor), typeof(Color), typeof(IconButton), Colors.White);

    /// <summary>
    /// Gets or sets the text color.
    /// </summary>
    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public static readonly BindableProperty IconColorProperty = BindableProperty.Create(
        nameof(IconColor), typeof(Color), typeof(IconButton), Colors.White);

    /// <summary>
    /// Gets or sets the icon color.
    /// </summary>
    public Color IconColor
    {
        get => (Color)GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }

    public static readonly BindableProperty ButtonBackgroundColorProperty = BindableProperty.Create(
        nameof(ButtonBackgroundColor), typeof(Color), typeof(IconButton), Colors.Blue);

    /// <summary>
    /// Gets or sets the background color of the button.
    /// </summary>
    public Color ButtonBackgroundColor
    {
        get => (Color)GetValue(ButtonBackgroundColorProperty);
        set => SetValue(ButtonBackgroundColorProperty, value);
    }

    public static readonly BindableProperty BorderBrushProperty = BindableProperty.Create(
        nameof(BorderBrush), typeof(Brush), typeof(IconButton), Brush.Transparent);

    /// <summary>
    /// Gets or sets the brush used for the button border.
    /// </summary>
    public Brush BorderBrush
    {
        get => (Brush)GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public static readonly BindableProperty BorderThicknessProperty = BindableProperty.Create(
        nameof(BorderThickness), typeof(double), typeof(IconButton), 0.0);

    /// <summary>
    /// Gets or sets the thickness of the button border.
    /// </summary>
    public double BorderThickness
    {
        get => (double)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius), typeof(double), typeof(IconButton), 10.0);

    /// <summary>
    /// Gets or sets the corner radius of the button.
    /// </summary>
    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public static readonly BindableProperty ButtonPaddingProperty = BindableProperty.Create(
        nameof(ButtonPadding), typeof(Thickness), typeof(IconButton), new Thickness(15, 10));

    /// <summary>
    /// Gets or sets the padding inside the button.
    /// </summary>
    public Thickness ButtonPadding
    {
        get => (Thickness)GetValue(ButtonPaddingProperty);
        set => SetValue(ButtonPaddingProperty, value);
    }

    public static readonly BindableProperty IsFullWidthProperty = BindableProperty.Create(
        nameof(IsFullWidth), typeof(bool), typeof(IconButton), true);

    /// <summary>
    /// Gets or sets a value indicating whether the button should take the full width available.
    /// If false, the button will adapt its size to its content.
    /// </summary>
    public bool IsFullWidth
    {
        get => (bool)GetValue(IsFullWidthProperty);
        set => SetValue(IsFullWidthProperty, value);
    }

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(IconButton), null);

    /// <summary>
    /// Gets or sets the command executed when the button is tapped.
    /// </summary>
    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(IconButton), null);

    /// <summary>
    /// Gets or sets the parameter passed to the command.
    /// </summary>
    public object CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }
    #endregion

    public IconButton()
    {
        InitializeComponent();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        UpdateVisualState();
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == IsEnabledProperty.PropertyName)
        {
            UpdateVisualState();
        }
    }

    private void UpdateVisualState()
    {
        VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled");
    }

    /// <summary>
    /// Handles the tap gesture on the button and provides visual feedback.
    /// </summary>
    private async void OnTapped(object sender, EventArgs e)
    {
        if (!IsEnabled) return;

        if (sender is VisualElement view)
        {
            // Visual feedback animation (pressed effect)
            await view.ScaleToAsync(0.95, 50, Easing.CubicOut);
            await view.ScaleToAsync(1.0, 50, Easing.CubicIn);
        }

        if (Command?.CanExecute(CommandParameter) == true)
        {
            Command.Execute(CommandParameter);
        }
    }
}
