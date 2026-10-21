using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace House.Of.Arbitration.Controls;

public partial class SideSheetControl : ContentView
{
    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
        nameof(IsOpen), 
        typeof(bool), 
        typeof(SideSheetControl), 
        false, 
        BindingMode.TwoWay,
        propertyChanged: OnIsOpenChanged
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

    public static readonly BindableProperty SheetBorderColorProperty = BindableProperty.Create(
        nameof(SheetBorderColor), 
        typeof(Color), 
        typeof(SideSheetControl), 
        Colors.White
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

    public Color SheetBorderColor
    {
        get => (Color)GetValue(SheetBorderColorProperty);
        set => SetValue(SheetBorderColorProperty, value);
    }

    public SideSheetControl()
    {
        InitializeComponent();
    }

    private static void OnIsOpenChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SideSheetControl control && newValue is bool isOpen)
        {
            control.UpdateState(isOpen);
        }
    }

    private async void UpdateState(bool isOpen)
    {
        if (SidePane == null || Overlay == null)
            return;

        double width = SheetWidth > 0 ? SheetWidth : 300.0;

        if (isOpen)
        {
            IsVisible = true;
            InputTransparent = false;

            Overlay.IsVisible = true;
            SidePane.IsVisible = true;
            Overlay.InputTransparent = false;
            SidePane.InputTransparent = false;

            if (SidePane.TranslationX >= 0)
            {
                SidePane.TranslationX = -width;
                Overlay.Opacity = 0;
            }

            try
            {
                await Task.WhenAll(
                    SidePane.TranslateToAsync(0, 0, 300, Easing.CubicOut),
                    Overlay.FadeToAsync(0.5, 300)
                );
            }
            catch
            {
                // Animation interrupted
            }
        }
        else
        {
            Overlay.InputTransparent = true;
            SidePane.InputTransparent = true;

            try
            {
                await Task.WhenAll(
                    SidePane.TranslateToAsync(-width, 0, 300, Easing.CubicIn),
                    Overlay.FadeToAsync(0, 300)
                );
            }
            catch
            {
                // Animation interrupted
            }

            if (!IsOpen)
            {
                SidePane.IsVisible = false;
                Overlay.IsVisible = false;
                IsVisible = false;
                InputTransparent = true;
            }
        }
    }
}
