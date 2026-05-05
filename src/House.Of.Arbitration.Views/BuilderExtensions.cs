#region Imports
using CommunityToolkit.Maui;
using House.Of.Arbitration.ViewModels;
using House.Of.Arbitration.ViewModels.Core;
using House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;
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
        builder.Services.AddTransient<MasterPage>();
        builder.Services.AddTransient<JudgePage>();
        builder.Services.AddTransient<CompetitionsPage>();
        builder.Services.AddTransient<CompetitorsPage>();
        builder.Services.AddTransient<DrawPage>();
        builder.Services.AddTransient<DragDropPage>();
        builder.Services.AddTransient<ServerPage>();

        builder.Services.AddTransientPopup<CategoryPopup, CategoryPopupViewModel>();
        builder.Services.AddTransientPopup<CompetitorPopup, CompetitorPopupViewModel>();
        builder.Services.AddTransientPopup<ConfirmationPopup, ConfirmationPopupViewModel>();
        builder.Services.AddTransientPopup<JudgePointsPopup, JudgePointsPopupViewModel>();
        builder.Services.AddTransientPopup<ServerSetupPopup, ServerSetupPopupViewModel>();
        return builder;
    }
}
