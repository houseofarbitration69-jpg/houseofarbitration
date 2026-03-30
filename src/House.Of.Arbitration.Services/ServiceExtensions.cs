using House.Of.Arbitration.Services.Abstractions;

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
        return builder;
    }
}
