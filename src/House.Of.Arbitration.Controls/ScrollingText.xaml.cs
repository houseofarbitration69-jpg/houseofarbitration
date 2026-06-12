using System;
using Microsoft.Maui.Controls;

namespace House.Of.Arbitration.Controls;

public partial class ScrollingText : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(ScrollingText), string.Empty, propertyChanged: OnTextChanged);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize), typeof(double), typeof(ScrollingText), 14.0);

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor), typeof(Color), typeof(ScrollingText), Colors.Black);

    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(
        nameof(FontAttributes), typeof(FontAttributes), typeof(ScrollingText), FontAttributes.None);

    public FontAttributes FontAttributes
    {
        get => (FontAttributes)GetValue(FontAttributesProperty);
        set => SetValue(FontAttributesProperty, value);
    }

    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(
        nameof(FontFamily), typeof(string), typeof(ScrollingText), string.Empty);

    public string FontFamily
    {
        get => (string)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public ScrollingText()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
    }

    private static void OnTextChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (ScrollingText)bindable;
        control.CheckAndStartAnimation();
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        CheckAndStartAnimation();
    }

    private bool _isAnimating;

    private async void CheckAndStartAnimation()
    {
        // Give it a small delay to ensure rendering is complete and measures are accurate
        await Task.Delay(150);

        if (Container.Width <= 0 || Label1.Width <= 0) return;

        // Use a small buffer to avoid jitter or rounding issues
        if (Label1.Width > Container.Width + 5)
        {
            ScrollLayout.HorizontalOptions = LayoutOptions.Start;
            if (_isAnimating) return;
            _isAnimating = true;
            Label2.IsVisible = true;

            while (_isAnimating && Label1.Width > Container.Width + 5)
            {
                ScrollLayout.TranslationX = 0;
                // Calculate duration based on width for constant speed (approx 50 pixels per second)
                uint duration = (uint)(Label1.Width * 20); 
                
                if (duration < 1000) duration = 5000;

                await ScrollLayout.TranslateToAsync(-(Label1.Width + ScrollLayout.Spacing), 0, duration, Easing.Linear);
                
                // Small pause at the end of a loop
                if (_isAnimating) await Task.Delay(500);
            }
        }
        else
        {
            StopAnimation();
        }
    }

    private void StopAnimation()
    {
        _isAnimating = false;
        Label2.IsVisible = false;
        ScrollLayout.CancelAnimations();
        ScrollLayout.TranslationX = 0;
        ScrollLayout.HorizontalOptions = LayoutOptions.Center;
    }
}
