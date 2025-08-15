using Microsoft.Extensions.Logging;
using Plugin.Firebase.CloudMessaging; // Jediný Firebase 'using', který potřebujeme zde
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

        // JEDINÁ VĚC, KTEROU PRO FIREBASE POTŘEBUJEME:
        // Zaregistrujeme si službu, aby ji ViewModel mohl použít.
        builder.Services.AddSingleton(CrossFirebaseCloudMessaging.Current);

        // Registrace našich vlastních tříd
        builder.Services.AddSingleton<ChatViewModel>();
        builder.Services.AddSingleton<MainPage>();

        // Přihlásíme se k události, která se spustí při přijetí notifikace,
        // POKUD JE APLIKACE OTEVŘENÁ (V POPŘEDÍ).
        CrossFirebaseCloudMessaging.Current.NotificationReceived += (sender, e) =>
        {
            // Pro jednoduchost zobrazíme obsah notifikace v systémovém alertu.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Shell.Current.DisplayAlert(e.Notification.Title, e.Notification.Body, "OK");
            });
        };

        return builder.Build();
    }
}