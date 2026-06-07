using House.Of.Arbitration.Models.Helpers;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;

namespace House.Of.Arbitration.Controls;

public partial class IconButton : ContentView
{
    #region Text Bindable Properties
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), 
        typeof(string), 
        typeof(IconButton), 
        string.Empty
    );

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly BindableProperty TextFontSizeProperty = BindableProperty.Create(
        nameof(TextFontSize), 
        typeof(double), 
        typeof(IconButton), 
        14.0
    );

    public double TextFontSize
    {
        get => (double)GetValue(TextFontSizeProperty);
        set => SetValue(TextFontSizeProperty, value);
    }

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor),
        typeof(Color),
        typeof(IconButton),
        Colors.White
    );

    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }
    #endregion

    #region Icon Bindable Properties
    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon), 
        typeof(string), 
        typeof(IconButton), 
        string.Empty
    );

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly BindableProperty IconFontSizeProperty = BindableProperty.Create(
        nameof(IconFontSize), 
        typeof(double), 
        typeof(IconButton), 
        18.0
    );

    public double IconFontSize
    {
        get => (double)GetValue(IconFontSizeProperty);
        set => SetValue(IconFontSizeProperty, value);
    }

    public ObservableCollection<IconInfo> InternalIcons { get; } = new();

    public static readonly BindableProperty IconsProperty = BindableProperty.Create(
        nameof(Icons), 
        typeof(IList), 
        typeof(IconButton), 
        null, 
        propertyChanged: OnIconsChanged
    );

    public IList Icons
    {
        get => (IList)GetValue(IconsProperty);
        set => SetValue(IconsProperty, value);
    }

    private static void OnIconsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (IconButton)bindable;

        if (oldValue is INotifyCollectionChanged oldCollection)
            oldCollection.CollectionChanged -= control.OnIconsCollectionChanged;

        if (newValue is INotifyCollectionChanged newCollection)
            newCollection.CollectionChanged += control.OnIconsCollectionChanged;

        control.UpdateInternalItems();
    }

    private void OnIconsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateInternalItems();
    }

    public static readonly BindableProperty IconFontFamilyProperty = BindableProperty.Create(
        nameof(IconFontFamily), 
        typeof(string), 
        typeof(IconButton), 
        FontHelper.FONTAWESOME_SOLID_NAME
    );

    public string IconFontFamily
    {
        get => (string)GetValue(IconFontFamilyProperty);
        set => SetValue(IconFontFamilyProperty, value);
    }

    public static readonly BindableProperty IconColorProperty = BindableProperty.Create(
        nameof(IconColor), 
        typeof(Color), 
        typeof(IconButton), 
        Colors.White
    );

    public Color IconColor
    {
        get => (Color)GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }
    #endregion

    #region Button Bindable Properties
    public static readonly BindableProperty ButtonBackgroundColorProperty = BindableProperty.Create(
        nameof(ButtonBackgroundColor), 
        typeof(Color), 
        typeof(IconButton), 
        Colors.Blue
    );

    public Color ButtonBackgroundColor
    {
        get => (Color)GetValue(ButtonBackgroundColorProperty);
        set => SetValue(ButtonBackgroundColorProperty, value);
    }

    public static readonly BindableProperty ButtonPaddingProperty = BindableProperty.Create(
        nameof(ButtonPadding), typeof(Thickness), typeof(IconButton), new Thickness(15, 10));

    public Thickness ButtonPadding
    {
        get => (Thickness)GetValue(ButtonPaddingProperty);
        set => SetValue(ButtonPaddingProperty, value);
    }

    public static readonly BindableProperty IsFullWidthProperty = BindableProperty.Create(
        nameof(IsFullWidth), typeof(bool), typeof(IconButton), true);

    public bool IsFullWidth
    {
        get => (bool)GetValue(IsFullWidthProperty);
        set => SetValue(IsFullWidthProperty, value);
    }
    #endregion

    #region Border Bindable Properties
    public static readonly BindableProperty BorderBrushProperty = BindableProperty.Create(
        nameof(BorderBrush), 
        typeof(Brush), 
        typeof(IconButton), 
        Brush.Transparent
    );

    public Brush BorderBrush
    {
        get => (Brush)GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public static readonly BindableProperty BorderThicknessProperty = BindableProperty.Create(
        nameof(BorderThickness), 
        typeof(double), 
        typeof(IconButton), 
        0.0
    );

    public double BorderThickness
    {
        get => (double)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius), 
        typeof(double), 
        typeof(IconButton), 
        10.0
    );

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }
    #endregion

    #region Commands
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(IconButton), null, propertyChanged: OnCommandChanged);

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    private static void OnCommandChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (IconButton)bindable;

        if (oldValue is ICommand oldCommand)
            oldCommand.CanExecuteChanged -= control.OnCommandCanExecuteChanged;

        if (newValue is ICommand newCommand)
        {
            newCommand.CanExecuteChanged += control.OnCommandCanExecuteChanged;
            control.OnCommandCanExecuteChanged(newCommand, EventArgs.Empty);
        }
    }

    private void OnCommandCanExecuteChanged(object? sender, EventArgs e)
    {
        if (Command != null)
        {
            IsEnabled = Command.CanExecute(CommandParameter);
        }
    }

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(IconButton), null, propertyChanged: OnCommandParameterChanged);

    public object CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    private static void OnCommandParameterChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (IconButton)bindable;
        control.OnCommandCanExecuteChanged(control.Command, EventArgs.Empty);
    }
    #endregion

    public IconButton()
    {
        // Initialisation par défaut pour XAML
        Icons = new ObservableCollection<IconInfo>();
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

    private void UpdateInternalItems()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            InternalIcons.Clear();
            if (Icons != null)
            {
                foreach (var item in Icons)
                {
                    if (item is IconInfo iconInfo)
                        InternalIcons.Add(iconInfo);
                    else if (item != null)
                        InternalIcons.Add(new IconInfo { Icon = item.ToString() ?? string.Empty, FontSize = IconFontSize });
                }
            }
        });
    }

    private async void OnTapped(object sender, EventArgs e)
    {
        if (!IsEnabled) return;

        if (sender is VisualElement view)
        {
            await view.ScaleToAsync(0.95, 50, Easing.CubicOut);
            await view.ScaleToAsync(1.0, 50, Easing.CubicIn);
        }

        if (Command?.CanExecute(CommandParameter) == true)
        {
            Command.Execute(CommandParameter);
        }
    }
}
