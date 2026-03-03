#region Imports
using CommunityToolkit.Maui.Core;
using House.Of.Arbitration.Data;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models.Helpers;
using House.Of.Arbitration.Services;
using House.Of.Arbitration.ViewModels;
using House.Of.Arbitration.Views;
using Microsoft.Extensions.Logging;
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
                    fonts.AddFont(FontHelper.OPENSANS_REGULAR_FILENAME, FontHelper.OPENSANS_REGULAR_NAME);
                    fonts.AddFont(FontHelper.OPENSANS_SEMIBOLD_FILENAME, FontHelper.OPENSANS_SEMIBOLD_NAME);

                    // Register FontAwesome
                    fonts.AddFont(FontHelper.FONTAWESOME_SOLID_FILENAME, FontHelper.FONTAWESOME_SOLID_NAME);

                    // Register Cutom
                    fonts.AddFont(FontHelper.CUSTOM_FILENAME, FontHelper.CUSTOM_NAME);
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
