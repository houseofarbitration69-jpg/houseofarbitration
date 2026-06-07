#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using House.Of.Arbitration.Data;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models.Helpers;
using House.Of.Arbitration.Services;
using House.Of.Arbitration.ViewModels;
using House.Of.Arbitration.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;
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
                .UseMauiCommunityToolkit(options =>
                {
                    options.SetShouldEnableSnackbarOnWindows(true);
                })
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

            var assembly = Assembly.GetExecutingAssembly();
            var executingNamespace = assembly.GetName().Name;

            using (var stream = assembly.GetManifestResourceStream($"{executingNamespace}.appsettings.json"))
            {
                if (stream != null)
                {
                    var config = new ConfigurationBuilder()
                                        .AddJsonStream(stream)
                                        .Build();

                    builder.Configuration.AddConfiguration(config);
                }
            }

#if DEBUG
        using (var stream = assembly.GetManifestResourceStream($"{executingNamespace}.appsettings.Development.json"))
        {
            if (stream != null)
            {
                var config = new ConfigurationBuilder()
                                    .AddJsonStream(stream)
                                    .Build();

                builder.Configuration.AddConfiguration(config);
            }
        }
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

            // Supprimer les bordures natives des Entry
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoBorder", (handler, view) =>
            {
#if ANDROID
                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#elif WINDOWS
                handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
#elif IOS || MACCATALYST
                handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
#endif
            });

            // Supprimer les bordures natives des DatePicker
            Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping("NoBorder", (handler, view) =>
            {
#if ANDROID
                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#elif WINDOWS
                handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
#endif
            });

            var app = builder.Build();

            app.SetDefaultCulture();

            return app;
        }
    }
}
