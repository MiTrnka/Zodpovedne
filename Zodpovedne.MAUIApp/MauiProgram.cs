using Plugin.Firebase.CloudMessaging;
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

        // Zaregistrujeme si službu, aby ji ViewModel mohl použít.
        builder.Services.AddSingleton(CrossFirebaseCloudMessaging.Current);

        // Registrace našich vlastních tříd
        builder.Services.AddSingleton<ChatViewModel>();
        builder.Services.AddSingleton<MainPage>();

        // Zpracování notifikace, KDYŽ JE APLIKACE V POPŘEDÍ
        // Přihlásíme se k události, která se spustí při přijetí notifikace,
        CrossFirebaseCloudMessaging.Current.NotificationReceived += (sender, e) =>
        {
            // Zajistíme, aby se kód vykonal na hlavním vlákně
            // Pro jednoduchost zobrazíme obsah notifikace v systémovém alertu.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Shell.Current.DisplayAlert(e.Notification.Title, e.Notification.Body, "OK");
            });
        };


        /* ZPRACOVÁNÍ KLIKNUTÍ NA NOTIFIKACI (KDYŽ APLIKACE NEBĚŽÍ NEBO JE NA POZADÍ)
        CrossFirebaseCloudMessaging.Current.NotificationTapped += (sender, e) =>
        {
            // Zajistíme, aby se kód vykonal na hlavním vlákně
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                // Zkontrolujeme, zda notifikace obsahuje námi definovaná data zaregistrovaná v serverové části v FirebaseNotificationService
                if (e.Notification.Data.TryGetValue("page", out var targetPage))
                {
                    // Pokud ano, použijeme Shell navigaci pro přesměrování
                    // na konkrétní stránku definovanou v datech.
                    // Dvojité lomítko (//) zajišťuje absolutní navigaci od kořene.
                    await Shell.Current.GoToAsync($"//{targetPage}");
                }
            });
        };*/

        return builder.Build();
    }
}

/*
## Scénář 1: První spuštění aplikace
Při úplně prvním spuštění aplikace na novém zařízení (nebo po smazání jejích dat) proběhne klíčový registrační proces, který se při dalších spuštěních již neopakuje.

Fáze 1: Aplikace se představuje (v Zodpovedne.MAUIApp)
    1. Zobrazení stránky: Uživateli se zobrazí hlavní stránka MainPage. Tím se v jejím code-behind souboru (MainPage.xaml.cs) automaticky spustí metoda OnAppearing.

    2. Inicializace logiky: Metoda OnAppearing zavolá metodu InitializeAsync v našem hlavním viewmodelu, třídě ChatViewModel.

    3. Spuštění registrace: InitializeAsync následně spustí příkaz RegisterForNotificationsCommand, který vykoná metodu RegisterForNotificationsAsync ve třídě ChatViewModel.

    4. Žádost o oprávnění: Protože se jedná o první spuštění na moderním Androidu, metoda RegisterForNotificationsAsync nejprve požádá systém o povolení zobrazovat notifikace. Využije k tomu naši pomocnou třídu PostNotificationsPermission, která reprezentuje toto systémové oprávnění.

    5. Získání adresy: Po udělení souhlasu aplikace kontaktuje servery Google/Firebase a vyžádá si svou unikátní "doručovací adresu" – FCM Registrační Token.

    6. Odeslání adresy na server: Aplikace vezme tento token a pomocí GraphQL mutace ho odešle na náš server, aby si ho uložil do svého "adresáře".

Fáze 2: Server si ukládá adresu (v Zodpovedne.GraphQL)
    1. Přijetí tokenu: Náš GraphQL server přijme požadavek z MAUI aplikace a spustí metodu RegisterFcmTokenAsync ve třídě Mutation.

    2. Uložení do databáze: Tato metoda vezme přijatý FCM token a uloží ho jako nový záznam do databázové tabulky FcmRegistrationTokens.

Výsledek: Po tomto jednorázovém procesu je zařízení oficiálně zaregistrováno v naší databázi a je připraveno přijímat notifikace. Při každém dalším spuštění aplikace se metoda RegisterForNotificationsAsync sice spustí, ale protože uživatel již oprávnění udělil a token je obvykle stále platný, neděje se nic viditelného.

## Scénář 2: Odeslání globální push notifikace
Tento proces se spustí, když uživatel klikne na tlačítko "Odeslat globální notifikaci".

Fáze 1: Příkaz z aplikace (v Zodpovedne.MAUIApp)
    1. Kliknutí na tlačítko: Stisknutí tlačítka na stránce MainPage aktivuje příkaz SendGlobalNotificationCommand ve třídě ChatViewModel.

    2. Spuštění metody: Příkaz vykoná metodu SendGlobalNotificationAsync.

    3. Sestavení požadavku: Tato metoda sestaví GraphQL mutaci pro odeslání notifikace. Klíčové je, že do tohoto požadavku přibalí i tajný API klíč, aby se prokázala serveru.

    4. Odeslání na server: Aplikace odešle kompletní požadavek na náš GraphQL server.

Fáze 2: Server zpracovává a odesílá (v Zodpovedne.GraphQL)
    1. Přijetí požadavku: Server přijme požadavek a spustí metodu SendGlobalNotificationAsync ve třídě Mutation.

    2. Bezpečnostní kontrola: Metoda nejprve zkontroluje, zda se API klíč z aplikace shoduje s klíčem uloženým v konfiguraci serveru. Pokud ne, okamžitě požadavek odmítne s chybou.

    3. Předání specializované službě: Pokud je klíč v pořádku, metoda předá úkol (odeslání notifikace) naší specializované třídě FirebaseNotificationService zavoláním její metody SendGlobalNotificationAsync.

    4. Načtení všech adres: FirebaseNotificationService se připojí k databázi a načte všechny FCM tokeny všech zařízení, která se kdy zaregistrovala.

    5. Příprava hromadné zprávy: Sestaví tzv. MulticastMessage – zprávu určenou pro hromadné rozeslání, která obsahuje titulek, text a seznam všech načtených tokenů.

    6. Odeslání do Firebase: Metoda nakonec předá tento balíček zpráv a adres přímo službě Firebase.

Fáze 3: Firebase doručuje zprávu (infrastruktura Firebase)
    1. Převzetí a doručení: Od tohoto momentu přebírá veškerou práci masivní infrastruktura Googlu. Firebase vezme naši zprávu a rozešle ji na všechna zařízení, jejichž tokeny byly na seznamu.

    2. Probuzení zařízení: Systém Android na cílových telefonech přijme signál od Firebase, "probudí se" a zobrazí uživateli systémovou push notifikaci s naším titulkem a textem.

    3. Zpětná vazba: Firebase pošle zpět našemu serveru stručný report o tom, na kolik zařízení se podařilo notifikaci úspěšně doručit.
*/