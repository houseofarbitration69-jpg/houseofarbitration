#region Imports
using CommunityToolkit.Maui.Views;
#if ANDROID
using Android.Content;
using Android.OS;
using Android.Views;
using AndroidX.Fragment.App;
#endif
#endregion

namespace House.Of.Arbitration.Views.Core.Helpers;

/// <summary>
/// Helper permettant de configurer la couleur de la barre de navigation Android pour les popups.
/// </summary>
public static class PopupHelper
{
    public const string DefaultNavigationBarColor = "#121212";

    /// <summary>
    /// Configure la couleur de la barre de navigation Android pour un Popup.
    /// </summary>
    /// <param name="popup">Le popup à configurer.</param>
    /// <param name="hexColor">Couleur hexadécimale (par défaut #121212).</param>
    public static void SetNavigationBarColor(this Popup popup, string hexColor = DefaultNavigationBarColor)
    {
#if ANDROID
        void StyleWindow(global::Android.Views.Window? window)
        {
            if (window == null)
                return;

            var color = Android.Graphics.Color.ParseColor(hexColor);
#pragma warning disable CA1422
            window.AddFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);
            window.ClearFlags(WindowManagerFlags.TranslucentNavigation);
            window.SetNavigationBarColor(color);
#pragma warning restore CA1422

            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var controller = window.DecorView?.WindowInsetsController;
                if (controller != null)
                {
                    controller.SetSystemBarsAppearance(
                        0,
                        (int)WindowInsetsControllerAppearance.LightNavigationBars);
                }
            }
        }

        void ApplyColor()
        {
            // 1. Si le handler natif est un Dialog
            if (popup.Handler?.PlatformView is Android.App.Dialog dialog && dialog.Window != null)
            {
                StyleWindow(dialog.Window);
            }

            // 2. Parcourir les DialogFragment actifs de l'Activity courante (utilisé par MAUI pour les pages modales / popups)
            if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is FragmentActivity fragmentActivity)
            {
                StyleWindow(fragmentActivity.Window);

                var fragments = fragmentActivity.SupportFragmentManager.Fragments;
                foreach (var frag in fragments)
                {
                    if (frag is DialogFragment dialogFragment && dialogFragment.Dialog?.Window != null)
                    {
                        StyleWindow(dialogFragment.Dialog.Window);
                    }
                }
            }
        }

        popup.HandlerChanged += (s, e) => ApplyColor();
        popup.Opened += (s, e) => ApplyColor();

        if (popup.Handler != null)
        {
            ApplyColor();
        }
#endif
    }
}
