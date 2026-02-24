using Microsoft.Extensions.Logging;
using BluetoothApp.Services;
using BluetoothApp.ViewModels; // Added this line
#if ANDROID
using BluetoothApp.Platforms.Android.Bluetooth;
#elif IOS
using BluetoothApp.Platforms.iOS.Bluetooth;
#endif

namespace BluetoothApp;

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

#if ANDROID
		builder.Services.AddSingleton<IBluetoothService, AndroidBluetoothService>();
		builder.Services.AddSingleton<IBluetoothServer, AndroidBluetoothServer>();
		builder.Services.AddSingleton<IBluetoothClient, AndroidBluetoothClient>();
#elif IOS
		builder.Services.AddSingleton<IBluetoothService, iOSBluetoothService>();
		builder.Services.AddSingleton<IBluetoothServer, iOSBluetoothServer>();
		builder.Services.AddSingleton<IBluetoothClient, iOSBluetoothClient>();
#endif
		builder.Services.AddTransient<MainViewModel>();
		return builder.Build();
	}
}
