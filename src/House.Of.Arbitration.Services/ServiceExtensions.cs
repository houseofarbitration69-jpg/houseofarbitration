#region Imports
using House.Of.Arbitration.Services.Abstractions;

#if ANDROID
using House.Of.Arbitration.Services.Platforms.Android.Bluetooth;
#endif

#endregion

namespace House.Of.Arbitration.Services;

/// <summary>
/// Extension methods for configuring localization services.
/// </summary>
public static class ServicesExtensions
{
    /// <summary>
    /// Registers localization services with the dependency injection container.
    /// </summary>
    /// <param name="builder">The <see cref="MauiAppBuilder"/> to add the services to.</param>
    /// <returns>The configured <see cref="MauiAppBuilder"/>.</returns>
    public static MauiAppBuilder RegisterServices(this MauiAppBuilder builder)
    {
        builder.Services.AddScoped<IWarningService, WarningService>();
        builder.Services.AddScoped<IAlertService, AlertService>();

#if ANDROID
        builder.Services.AddSingleton<IBluetoothService, BluetoothService>();
        builder.Services.AddSingleton<IBluetoothServer, BluetoothServer>();
        builder.Services.AddSingleton<IBluetoothClient, BluetoothClient>();
#endif

        return builder;
    }
}
