using System.Windows.Input;

namespace House.Of.Arbitration.Controls;

public partial class SideSheetControl : ContentView
{
    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
        nameof(IsOpen), 
        typeof(bool), 
        typeof(SideSheetControl), 
        false, 
        BindingMode.TwoWay
    );

    public static readonly BindableProperty SheetContentProperty = BindableProperty.Create(
        nameof(SheetContent), 
        typeof(View), 
        typeof(SideSheetControl)
    );

    public static readonly BindableProperty CloseCommandProperty = BindableProperty.Create(
        nameof(CloseCommand), 
        typeof(ICommand), 
        typeof(SideSheetControl)
    );

    public static readonly BindableProperty SheetWidthProperty = BindableProperty.Create(
        nameof(SheetWidth), 
        typeof(double), 
        typeof(SideSheetControl), 
        300.0
    );

    public static readonly BindableProperty SheetBackgroundColorProperty = BindableProperty.Create(
        nameof(SheetBackgroundColor), 
        typeof(Color), 
        typeof(SideSheetControl), 
        Color.FromArgb("#222")
    );

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public View SheetContent
    {
        get => (View)GetValue(SheetContentProperty);
        set => SetValue(SheetContentProperty, value);
    }

    public ICommand CloseCommand
    {
        get => (ICommand)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public double SheetWidth
    {
        get => (double)GetValue(SheetWidthProperty);
        set => SetValue(SheetWidthProperty, value);
    }

    public Color SheetBackgroundColor
    {
        get => (Color)GetValue(SheetBackgroundColorProperty);
        set => SetValue(SheetBackgroundColorProperty, value);
    }

    public SideSheetControl()
	{
		InitializeComponent();
	}
}
