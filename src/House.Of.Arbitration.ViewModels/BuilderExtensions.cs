#region Imports
using House.Of.Arbitration.ViewModels.Wizard.Competition;
using House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;
using House.Of.Arbitration.ViewModels.Competition;
using House.Of.Arbitration.ViewModels.Core;
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
        builder.Services.AddTransient<MasterViewModel>();
        builder.Services.AddTransient<SlaveViewModel>();
        builder.Services.AddTransient<CompetitionsViewModel>();

        builder.Services.AddTransient<CompetitionWizardViewModel>();
        builder.Services.AddTransient<SettingsStepViewModel>();

        builder.Services.AddTransient<CompetitorsPageViewModel>();
        builder.Services.AddTransient<CompetitorPopupViewModel>();

        builder.Services.AddTransient<ConfirmationPopupViewModel>();

        builder.Services.AddTransient<DrawPageViewModel>();
        builder.Services.AddTransient<DragDropPageViewModel>();

        return builder;
    }
}
