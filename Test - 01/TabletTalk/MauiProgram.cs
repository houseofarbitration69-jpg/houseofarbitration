using Microsoft.Extensions.Logging;
using TabletTalk.Services;
using TabletTalk.ViewModels;

namespace TabletTalk;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif
        // Services
        builder.Services.AddSingleton<WifiService>();

#if ANDROID || IOS
        // Enregistre l'implémentation native du serveur GATT pour la plateforme cible
        builder.Services.AddSingleton<IGattServer, GattServer>();
#endif

        builder.Services.AddSingleton<BluetoothStarService>(); // Doit être après IGattServer
        builder.Services.AddSingleton<ConnectivityManager>();

        // ViewModel & Page
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainPage>();

		return builder.Build();
	}
}
