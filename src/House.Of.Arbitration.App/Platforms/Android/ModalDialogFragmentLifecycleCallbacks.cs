#if ANDROID
using System;
using Android.OS;
using AndroidX.Fragment.App;

namespace House.Of.Arbitration.App.Platforms.Android;

/// <summary>
/// Intercepte le cycle de vie de tous les DialogFragment (pages modales et popups MAUI)
/// pour garantir que la barre de navigation système conserve la couleur configurée.
/// </summary>
public class ModalDialogFragmentLifecycleCallbacks : FragmentManager.FragmentLifecycleCallbacks
{
    public const string DefaultColorHex = "#121212";

    public override void OnFragmentStarted(FragmentManager fm, global::AndroidX.Fragment.App.Fragment f)
    {
        base.OnFragmentStarted(fm, f);
        ApplyWindowStyling(f);
    }

    public override void OnFragmentResumed(FragmentManager fm, global::AndroidX.Fragment.App.Fragment f)
    {
        base.OnFragmentResumed(fm, f);
        ApplyWindowStyling(f);
    }

    public override void OnFragmentViewCreated(FragmentManager fm, global::AndroidX.Fragment.App.Fragment f, global::Android.Views.View v, Bundle? savedInstanceState)
    {
        base.OnFragmentViewCreated(fm, f, v, savedInstanceState);
        ApplyWindowStyling(f);
    }

    private static void ApplyWindowStyling(global::AndroidX.Fragment.App.Fragment f)
    {
        if (f is DialogFragment dialogFragment)
        {
            var dialog = dialogFragment.Dialog;
            if (dialog?.Window is { } window)
            {
                ApplyToWindow(window);
            }

            if (dialog != null)
            {
                dialog.ShowEvent -= OnDialogShow;
                dialog.ShowEvent += OnDialogShow;
            }
        }
    }

    private static void OnDialogShow(object? sender, EventArgs e)
    {
        if (sender is global::Android.App.Dialog dialog && dialog.Window is { } window)
        {
            ApplyToWindow(window);
        }
    }

    public static void ApplyToWindow(global::Android.Views.Window? window, string hexColor = DefaultColorHex)
    {
        if (window == null)
            return;

        var color = global::Android.Graphics.Color.ParseColor(hexColor);

#pragma warning disable CA1422
        window.AddFlags(global::Android.Views.WindowManagerFlags.DrawsSystemBarBackgrounds);
        window.ClearFlags(global::Android.Views.WindowManagerFlags.TranslucentNavigation);
        window.SetNavigationBarColor(color);
#pragma warning restore CA1422

        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var controller = window.DecorView?.WindowInsetsController;
            if (controller != null)
            {
                controller.SetSystemBarsAppearance(
                    0,
                    (int)global::Android.Views.WindowInsetsControllerAppearance.LightNavigationBars);
            }
        }
    }
}
#endif
