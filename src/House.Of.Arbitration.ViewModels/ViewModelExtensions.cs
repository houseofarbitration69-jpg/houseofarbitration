namespace House.Of.Arbitration.ViewModels;

public static class ViewModelsExtensions
{
    /// <summary>
    /// Registers localization services with the dependency injection container.
    /// </summary>
    /// <param name="builder">The <see cref="MauiAppBuilder"/> to add the services to.</param>
    /// <returns>The configured <see cref="MauiAppBuilder"/>.</returns>
    public static MauiAppBuilder RegisterViewModels(this MauiAppBuilder builder)
    {
        return builder;
    }
}
