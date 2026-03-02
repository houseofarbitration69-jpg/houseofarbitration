using System.Globalization;

namespace House.Of.Arbitration.Localization;

/// <summary>
/// Extension methods for configuring localization services.
/// </summary>
public static class LocalizationExtensions
{
    /// <summary>
    /// Registers localization services with the dependency injection container.
    /// </summary>
    /// <param name="builder">The <see cref="MauiAppBuilder"/> to add the services to.</param>
    /// <returns>The configured <see cref="MauiAppBuilder"/>.</returns>
    public static MauiAppBuilder RegisterLocalization(this MauiAppBuilder builder)
    {
        builder.Services.AddSingleton(LocalizationResourceManager.Instance);
        builder.Services.AddSingleton<ResourceProvider>();

        return builder;
    }

    /// <summary>
    /// Sets the default culture for the application at startup.
    /// </summary>
    /// <param name="app">The <see cref="MauiApp"/>.</param>
    public static void SetDefaultCulture(this MauiApp app)
    {
        var culture = new CultureInfo("fr-FR"); // or load from settings
        LocalizationResourceManager.Instance.SetCulture(culture);
    }
}
