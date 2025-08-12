using Microsoft.Extensions.Logging;
using Zodpovedne.MAUIApp.ViewModels;

namespace Zodpovedne.MAUIApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Zaregistrujeme naši stránku a ViewModel
        // 'AddSingleton' znamená, že se vytvoří jen jedna instance po celou dobu běhu aplikace.
        builder.Services.AddSingleton<ChatViewModel>();
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}
