#region Imports
using House.Of.Arbitration.Views.Competition;
using House.Of.Arbitration.Views.Wizard.Competition.Steps;
#endregion

namespace House.Of.Arbitration.Views;

public static class BuilderExtensions
{
    /// <summary>
    /// Registers localization services with the dependency injection container.
    /// </summary>
    /// <param name="builder">The <see cref="MauiAppBuilder"/> to add the services to.</param>
    /// <returns>The configured <see cref="MauiAppBuilder"/>.</returns>
    public static MauiAppBuilder RegisterViews(this MauiAppBuilder builder)
    {
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<CompetitionsPage>();
        builder.Services.AddTransient<CompetitorPage>();

        return builder;
    }
}
