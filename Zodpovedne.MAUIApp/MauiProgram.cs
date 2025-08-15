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

/*
 Cesta push notifikace: Detailní průvodce implementací
Cílem našeho snažení bylo vytvořit systém, kde náš server (Zodpovedne.GraphQL) dokáže poslat zprávu, která se v reálném čase objeví jako systémová notifikace na telefonech uživatelů s nainstalovanou aplikací (Zodpovedne.MAUIApp). Abychom toho dosáhli, potřebovali jsme spolehlivého prostředníka, který zprávy doručí. Tímto prostředníkem se stala služba Firebase od Googlu.

Celou architekturu si můžeme představit jako moderní poštovní službu 📮:

Firebase Konzole: Centrální pošta, kde všechno zakládáme a spravujeme.

MAUI Aplikace: Zákazník, který si na poště zaregistruje svou doručovací adresu.

GraphQL Server: Odesílatel, který na poštu přinese dopis a seznam adres, kam ho chce doručit.

## 1. Základní kámen: Nastavení ve Firebase Konzoli
Vše začalo v online prostředí Firebase. Založili jsme si zde nový projekt, který slouží jako centrální uzel pro veškerou komunikaci. V rámci tohoto projektu jsme museli provést dva klíčové kroky pro propojení s našimi aplikacemi.

Registrace mobilní aplikace a soubor google-services.json
Nejprve jsme museli naši MAUI aplikaci ve Firebase "zaregistrovat". Během tohoto procesu jsme Firebase sdělili unikátní identifikátor naší aplikace (v našem případě cz.discussion.app), který je definován v jejím projektovém souboru.

Po dokončení registrace nám Firebase vygeneroval konfigurační soubor google-services.json. Tento soubor si můžeme představit jako občanský průkaz mobilní aplikace. Obsahuje unikátní klíče a ID, pomocí kterých se naše MAUI aplikace při spuštění prokazuje serverům Googlu. Tím jim říká: "Ahoj, jsem aplikace 'Discussion' patřící k tomuto Firebase projektu, prosím o přístup ke službám." Tento soubor jsme stáhli a vložili do kořenového adresáře projektu Zodpovedne.MAUIApp a nastavili mu speciální "build akci", aby ho systém Android správně zpracoval při sestavování aplikace.

Vytvoření servisního účtu a soubor firebase-credentials.json
Dále jsme potřebovali způsob, jak se k Firebase mohl připojit náš server. Na rozdíl od mobilní aplikace, která je jen klientem, náš server potřebuje administrátorská práva – musí mít oprávnění posílat notifikace jménem celého projektu.

K tomu slouží tzv. "servisní účet". Ve Firebase jsme vytvořili tento speciální účet a vygenerovali pro něj privátní klíč ve formě souboru firebase-credentials.json. Tento soubor je mnohem citlivější než ten předchozí – jsou to v podstatě klíče od království 🔑. Umožňuje jakémukoliv serveru, který ho vlastní, plně ovládat služby našeho Firebase projektu. Tento soubor jsme proto nahráli přímo na náš produkční server k projektu Zodpovedne.GraphQL a zajistili, aby se nikdy nedostal do veřejného repozitáře.

## 2. Příjemce: Logika v mobilní aplikaci (Zodpovedne.MAUIApp)
S připravenou infrastrukturou ve Firebase jsme se přesunuli k mobilní aplikaci, kterou jsme museli naučit, jak se stát příjemcem notifikací.

Registrační proces
Hlavní úkol aplikace je získat svou unikátní "doručovací adresu" a sdělit ji našemu serveru. Tento proces probíhá automaticky při startu aplikace:

Žádost o oprávnění: Protože moderní Android vyžaduje souhlas uživatele se zobrazováním notifikací, aplikace nejprve zobrazila systémový dialog, kde uživatele požádala o povolení. K tomu jsme využili naši pomocnou třídu PostNotificationsPermission, která tuto systémovou žádost zapouzdřila pro použití v .NET MAUI.

Získání FCM Tokenu: Po udělení souhlasu aplikace komunikovala s Firebase (pomocí google-services.json) a vyžádala si unikátní FCM Registrační Token. Tento dlouhý řetězec je onou unikátní adresou pro konkrétní instalaci aplikace na konkrétním zařízení.

Odeslání tokenu na server: Aplikace následně zavolala naši GraphQL mutaci a předala serveru svůj nově získaný FCM token, aby si ho server mohl uložit do své databáze – do svého "adresáře".

Ukládání přezdívky
Aby byla aplikace uživatelsky přívětivější, implementovali jsme ukládání zadané přezdívky do trvalého úložiště telefonu (Preferences). Kdykoliv uživatel změní svou přezdívku v textovém poli, automaticky se uloží. Při příštím spuštění aplikace se přezdívka z tohoto úložiště opět načte, takže ji nemusí vyplňovat znovu.

## 3. Odesílatel: Logika na serveru (Zodpovedne.GraphQL)
Náš server měl dva hlavní úkoly: sbírat adresy od klientů a následně na ně odesílat zprávy.

Inicializace a sběr adres
Při svém startu si server jednorázově načetl citlivý soubor firebase-credentials.json a pomocí něj se autorizoval u Firebase jako administrátor. Dále jsme na serveru vytvořili databázovou tabulku, která sloužila jako náš adresář. Kdykoliv mobilní aplikace zavolala mutaci pro registraci tokenu, server tento token vzal a uložil do této tabulky.

Proces odeslání globální notifikace
Když jsme chtěli odeslat notifikaci (ať už z testovacího prostředí, nebo tlačítkem v aplikaci), spustil se na serveru následující proces:

Ověření API klíčem: Nejprve server zkontroloval, zda požadavek na odeslání obsahuje správný tajný API klíč. Tím jsme zajistili, že notifikaci může "odpálit" pouze naše aplikace, a ne nějaký robot z internetu.

Načtení adres: Server se podíval do své databáze a načetl všechny uložené FCM tokeny.

Sestavení zprávy: Vytvořil obsah notifikace – titulek a text.

Předání poště: Nakonec server předal Firebase Admin SDK celý balík: obsah zprávy a kompletní seznam adres (tokenů), na které se má doručit.

Od tohoto momentu převzal veškerou těžkou práci Firebase. Jeho globální infrastruktura zajistila, že se zpráva spolehlivě a téměř okamžitě doručila na všechna zařízení v našem seznamu, ať už byla kdekoliv na světě.
*/