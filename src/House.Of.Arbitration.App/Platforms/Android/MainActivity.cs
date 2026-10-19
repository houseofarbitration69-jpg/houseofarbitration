using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Maui.Platform;

namespace House.Of.Arbitration.App
{
    [Activity(
        Theme = "@style/Maui.SplashTheme", 
        MainLauncher = true, 
        LaunchMode = LaunchMode.SingleTop, 
        WindowSoftInputMode = SoftInput.AdjustResize,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)
    ]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // 1. Définir la couleur souhaitée (Exemple ici : un gris foncé)
            var CustomColor = Microsoft.Maui.Graphics.Color.FromArgb("#121212").ToPlatform();

            if (Window != null)
            {
                // 2. Changer la couleur de la barre de navigation du bas
                Window.SetNavigationBarColor(CustomColor);

                // 3. Optionnel : Adapter la couleur des icônes de la barre (boutons retour/accueil)
                // Si votre fond est clair, passez à 'true' pour que les boutons deviennent sombres.
                // Si votre fond est sombre, passez à 'false' pour que les boutons restent blancs.
                var windowInsetsController = Window.DecorView.WindowInsetsController;
                if (windowInsetsController != null)
                {
                    windowInsetsController.SetSystemBarsAppearance(
                        0, // Utilisez WindowInsetsControllerAppearance.LightNavigationBars si fond clair
                        (int)WindowInsetsControllerAppearance.LightNavigationBars);
                }
            }
        }
    }
}
