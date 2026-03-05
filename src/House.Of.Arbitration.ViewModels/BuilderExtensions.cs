#region Imports
using House.Of.Arbitration.ViewModels.Wizard.Competition;
using House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;
#endregion

namespace House.Of.Arbitration.ViewModels;

public static class BuilderExtensions
{
    /// <summary>
    /// Registers localization services with the dependency injection container.
    /// </summary>
    /// <param name="builder">The <see cref="MauiAppBuilder"/> to add the services to.</param>
    /// <returns>The configured <see cref="MauiAppBuilder"/>.</returns>
    public static MauiAppBuilder RegisterViewModels(this MauiAppBuilder builder)
    {
        builder.Services.AddTransient<HomeViewModel>();

        builder.Services.AddTransient<CompetitionWizardViewModel>();
        builder.Services.AddTransient<SettingsStepViewModel>();

        return builder;
    }
}
