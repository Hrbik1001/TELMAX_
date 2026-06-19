using Microsoft.Extensions.Logging;
using PIDMobileSpeaker.Services;

namespace PIDMobileSpeaker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>();

        builder.Services.AddSingleton<AppDataService>();
        builder.Services.AddSingleton<AudioService>();
        builder.Services.AddSingleton<RouteProgressService>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
