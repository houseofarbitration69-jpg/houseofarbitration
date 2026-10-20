using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using House.Of.Arbitration.App.Platforms.Android;
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

            ModalDialogFragmentLifecycleCallbacks.ApplyToWindow(Window);

            SupportFragmentManager.RegisterFragmentLifecycleCallbacks(
                new ModalDialogFragmentLifecycleCallbacks(), 
                true);
        }

        protected override void OnResume()
        {
            base.OnResume();

            ModalDialogFragmentLifecycleCallbacks.ApplyToWindow(Window);
        }
    }
}
