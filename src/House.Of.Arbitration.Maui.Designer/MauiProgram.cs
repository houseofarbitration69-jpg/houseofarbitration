using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace House.Of.Arbitration.Maui.Designer
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("opensans_regular.ttf", "opensans_regular");
                    fonts.AddFont("opensans_semibold.ttf", "opensans_semibold");
                    fonts.AddFont("fontawesome_solid.otf", "fontawesome_solid");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
