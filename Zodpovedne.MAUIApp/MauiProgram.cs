using Microsoft.Extensions.Logging;
using Zodpovedne.MAUIApp.Services;
using Zodpovedne.MAUIApp.ViewModels;
using Zodpovedne.MAUIApp.Views;

namespace Zodpovedne.MAUIApp
{
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

            builder.Services.AddSingleton<GraphQLService>();
            builder.Services.AddSingleton<ChatViewModel>();
            builder.Services.AddSingleton<ChatPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
