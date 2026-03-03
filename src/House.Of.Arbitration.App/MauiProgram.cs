#region Imports
using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.Logging;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Services;
using House.Of.Arbitration.ViewModels;
using House.Of.Arbitration.Views;
using House.Of.Arbitration.Data;
#endregion

namespace House.Of.Arbitration.App
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkitCore()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            // Register Localization
            builder.RegisterLocalization();

            // Register Services
            builder.RegisterServices();

            // Register ViewModel
            builder.RegisterViewModels();

            // Register Views
            builder.RegisterViews();

            // Register DbContext
            builder.RegisterDbContext();

            return builder.Build();
        }
    }
}
