using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
// Potřebujeme přístup k Preferences
using Microsoft.Maui.Storage;
using Plugin.Firebase.CloudMessaging;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using Zodpovedne.MAUIApp.Models;

namespace Zodpovedne.MAUIApp.ViewModels;

/// <summary>
/// Hlavní ViewModel pro chatovací stránku aplikace.
/// Tato třída spravuje veškerou logiku související s chatem: načítání a odesílání zpráv,
/// zpracování uživatelského vstupu a registraci zařízení pro příjem push notifikací.
/// </summary>
public partial class ChatViewModel : ObservableObject
{
    /// <summary>
    /// Statická instance HttpClient pro efektivní a opakované síťové požadavky.
    /// </summary>
    private static readonly HttpClient client = new();

    /// <summary>
    /// Konstantní adresa produkčního GraphQL API serveru.
    /// </summary>
    private const string ApiUrl = "https://api.discussion.cz/graphql";

    /// <summary>
    /// Služba pro interakci s Firebase Cloud Messaging, získaná přes dependency injection.
    /// </summary>
    private readonly IFirebaseCloudMessaging _firebaseCloudMessaging;

    /// <summary>
    /// Konstanta pro API klíč pro autentizaci požadavků (požadavek na odeslání push notifikace všem)
    /// </summary>
    private const string ApiKey = "primitivnizabezpeceniprotispamerum";

    /// <summary>
    /// GraphQL dotaz pro načtení všech zpráv z diskuze.
    /// </summary>
    private const string GetAllMessagesQuery = @"
        query NactiVsechnyZpravy {
            freeMessages {
                id
                nickname
                text
                createdUtc
            }
        }";

    /// <summary>
    /// Šablona pro GraphQL mutaci, která vytváří novou zprávu.
    /// Používá formátovací zástupné symboly {0} pro přezdívku a {1} pro text.
    /// </summary>
    // Najděte a nahraďte původní šablonu touto:
    private const string CreateMessageMutationTemplate = @"
    mutation CreateMessage($msgInput: AddFreeMessageInput!, $apiKey: String!) {
        addFreeMessage(input: $msgInput, apiKey: $apiKey) {
            id
        }
    }";

    /// <summary>
    /// Šablona pro GraphQL mutaci, která registruje FCM token zařízení na serveru.
    /// Zástupný symbol {0} je určen pro samotný token.
    /// </summary>SendGlobalNotificationAsync
    private const string RegisterFcmTokenMutationTemplate = @"
        mutation RegisterFcmToken {{
            registerFcmToken(token: ""{0}"")
        }}";

    /// <summary>
    /// GraphQL mutace pro odeslání globální push notifikace na všechna registrovaná zařízení.
    /// </summary>
    private const string SendNotificationMutation = @"
        mutation Notifikace($apiKey: String!) {
            sendGlobalNotification(title: ""Nová zpráva"", body: ""Právě přišla nová zpráva do chatu!"", apiKey: $apiKey)
        }";

    /// <summary>
    /// Příznak indikující, zda právě probíhá asynchronní operace (např. načítání dat).
    /// Je propojen s UI, aby bylo možné zakázat ovládací prvky a zabránit duplicitním voláním.
    /// </summary>
    [ObservableProperty]
    private bool isBusy;

    /// <summary>
    /// Přezdívka uživatele, vázaná na vstupní pole v uživatelském rozhraní.
    /// </summary>
    [ObservableProperty]
    private string nickname;

    /// <summary>
    /// Text nové zprávy, vázaný na vstupní pole v uživatelském rozhraní.
    /// </summary>
    [ObservableProperty]
    private string messageText;

    /// <summary>
    /// Kolekce zpráv zobrazených v UI. Použití ObservableCollection zajišťuje,
    /// že se uživatelské rozhraní automaticky aktualizuje při přidání nebo odebrání položek.
    /// </summary>
    public ObservableCollection<FreeMessage> Messages { get; } = new();

    /// <summary>
    /// Konstruktor ViewModelu. Přijímá službu IFirebaseCloudMessaging
    /// prostřednictvím dependency injection a načítá uloženou přezdívku.
    /// </summary>
    public ChatViewModel(IFirebaseCloudMessaging firebaseCloudMessaging)
    {
        _firebaseCloudMessaging = firebaseCloudMessaging;
        // NOVINKA: Načtení přezdívky při startu ViewModelu
        LoadNickname();
    }

    /// <summary>
    /// Metoda, kterou automaticky vygeneruje CommunityToolkit.Mvvm a zavolá se
    /// pokaždé, když se změní hodnota vlastnosti 'Nickname'.
    /// </summary>
    /// <param name="value">Nová hodnota přezdívky.</param>
    partial void OnNicknameChanged(string value)
    {
        // Uložíme novou hodnotu do trvalého úložiště zařízení pod klíčem 'user_nickname'.
        Preferences.Set("user_nickname", value);
    }

    /// <summary>
    /// Načte dříve uloženou přezdívku z úložiště zařízení.
    /// Pokud žádná uložena není, vrátí prázdný řetězec.
    /// </summary>
    private void LoadNickname()
    {
        Nickname = Preferences.Get("user_nickname", string.Empty);
    }

    /// <summary>
    /// Inicializační metoda, která se volá při zobrazení stránky.
    /// Spouští počáteční načtení zpráv a registraci pro notifikace.
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadMessagesCommand.ExecuteAsync(null);
        await RegisterForNotificationsCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Příkaz pro načtení zpráv ze serveru. Zabraňuje vícenásobnému spuštění
    /// pomocí příznaku IsBusy a v případě chyby zobrazí upozornění.
    /// </summary>
    [RelayCommand]
    private async Task LoadMessagesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await FetchAndDisplayMessagesAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Chyba načítání", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Příkaz pro odeslání nové zprávy. Provede validaci vstupu, sestaví a odešle
    /// GraphQL mutaci. Po úspěšném odeslání vymaže textové pole a znovu načte zprávy.
    /// </summary>
    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(Nickname) || string.IsNullOrWhiteSpace(MessageText))
        {
            await Shell.Current.DisplayAlert("Chyba", "Musíte vyplnit přezdívku i text zprávy.", "OK");
            return;
        }

        IsBusy = true;
        try
        {
            // Sestavíme dotaz, kde 'msgInput' je nyní celý objekt
            var request = new
            {
                query = CreateMessageMutationTemplate,
                variables = new
                {
                    // Vytvoříme vnořený objekt, který přesně odpovídá
                    // typu AddFreeMessageInput na serveru
                    msgInput = new { nickname = Nickname, text = MessageText },
                    apiKey = ApiKey
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(ApiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                if (jsonResponse.Contains("errors"))
                {
                    await Shell.Current.DisplayAlert("Chyba od GraphQL", jsonResponse, "OK");
                }
                else
                {
                    MessageText = string.Empty;
                    await FetchAndDisplayMessagesAsync();
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                await Shell.Current.DisplayAlert("Chyba API", $"Chyba: {response.StatusCode}\n{errorContent}", "OK");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Výjimka", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Příkaz, který se postará o kompletní proces registrace zařízení pro příjem notifikací.
    /// Na Androidu 13+ nejprve požádá o systémové oprávnění, poté získá FCM token
    /// a odešle ho na server k uložení.
    /// </summary>
    [RelayCommand]
    private async Task RegisterForNotificationsAsync()
    {
        try
        {
            // Následující blok kódu se zkompiluje a spustí pouze při sestavování aplikace pro platformu Android.
#if ANDROID
            // Zkontrolujeme, zda aplikace běží na Androidu verze 13 (API 33) nebo vyšší.
            // Pouze tyto verze vyžadují explicitní žádost o oprávnění k zasílání notifikací.
            if (DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.Version.Major >= 13)
            {
                // Asynchronně požádáme uživatele o udělení oprávnění k zasílání notifikací.
                // Používáme k tomu naši vlastní třídu PostNotificationsPermission, která toto oprávnění reprezentuje.
                var status = await Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<Permissions.PostNotificationsPermission>();

                // Pokud uživatel oprávnění neudělil...
                if (status != Microsoft.Maui.ApplicationModel.PermissionStatus.Granted)
                {
                    // ...zobrazíme mu upozornění a ukončíme metodu. Bez oprávnění nemá smysl pokračovat.
                    await Shell.Current.DisplayAlert("Oprávnění zamítnuto", "Bez udělení oprávnění nemůžete přijímat notifikace.", "OK");
                    return;
                }
            }
#endif
            // Ověříme, zda jsou na zařízení dostupné potřebné služby Google Play pro fungování Firebase.
            await _firebaseCloudMessaging.CheckIfValidAsync();

            // Požádáme Firebase o unikátní registrační token pro tuto konkrétní instalaci aplikace.
            // Tento token slouží jako unikátní adresa pro doručování notifikací.
            var token = await _firebaseCloudMessaging.GetTokenAsync();

            // Pokud se z nějakého důvodu nepodařilo token získat (např. problém s připojením)...
            if (string.IsNullOrEmpty(token))
            {
                // ...zobrazíme uživateli chybu a ukončíme metodu.
                await Shell.Current.DisplayAlert("Chyba Firebase", "Nepodařilo se získat FCM token.", "OK");
                return;
            }

            // Sestavíme text GraphQL mutace pro registraci tokenu na našem serveru.
            var query = string.Format(RegisterFcmTokenMutationTemplate, token);

            // Vytvoříme anonymní objekt, který bude převeden na JSON tělo požadavku.
            var request = new { query };

            // Převedeme (serializujeme) C# objekt na JSON řetězec a připravíme ho pro odeslání.
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            // Odešleme HTTP POST požadavek na náš GraphQL server.
            var response = await client.PostAsync(ApiUrl, content);

            // Pokud odpověď ze serveru neobsahuje úspěšný stavový kód (např. 404, 500)...
            if (!response.IsSuccessStatusCode)
            {
                // ...přečteme chybovou zprávu z těla odpovědi a zobrazíme ji uživateli.
                var error = await response.Content.ReadAsStringAsync();
                await Shell.Current.DisplayAlert("Chyba registrace tokenu", error, "OK");
            }
        }
        catch (Exception ex)
        {
            // Pokud během celého procesu dojde k jakékoliv jiné výjimce (např. problém se sítí),
            // odchytíme ji a zobrazíme její zprávu uživateli.
            await Shell.Current.DisplayAlert("Výjimka při registraci", ex.Message, "OK");
        }
    }

    /// <summary>
    /// Příkaz, který "odpálí" odeslání globální notifikace všem uživatelům.
    /// Odešle na server GraphQL mutaci a informuje uživatele o odeslání příkazu.
    /// </summary>
    [RelayCommand]
    private async Task SendGlobalNotificationAsync()
    {
        // Pokud již probíhá jiná operace, metodu okamžitě ukončíme, abychom zabránili duplicitním voláním.
        if (IsBusy) return;

        // Nastavíme příznak "zaneprázdněno" na true, což může v UI například zakázat tlačítko.
        IsBusy = true;

        // Celou logiku obalíme do try-catch-finally bloku pro robustní ošetření chyb a správu stavu.
        try
        {
            // Připravíme C# objekt, který reprezentuje tělo našeho GraphQL požadavku.
            // Obsahuje samotný text mutace a objekt s proměnnými (zde náš tajný API klíč).
            var request = new
            {
                query = SendNotificationMutation,
                variables = new { apiKey = ApiKey }
            };

            // Převedeme (serializujeme) C# objekt na JSON řetězec a připravíme ho pro odeslání přes HTTP.
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            // Asynchronně odešleme HTTP POST požadavek na náš GraphQL server a uložíme si odpověď.
            var response = await client.PostAsync(ApiUrl, content);

            // Zkontrolujeme, zda byl samotný HTTP přenos úspěšný (např. status 200 OK).
            if (response.IsSuccessStatusCode)
            {
                // Přečteme textovou odpověď (JSON) z těla HTTP odpovědi.
                var jsonResponse = await response.Content.ReadAsStringAsync();

                // Zkontrolujeme, zda JSON odpověď neobsahuje klíčové slovo "errors".
                // GraphQL server totiž může vrátit HTTP 200 OK, i když samotná operace selhala.
                if (jsonResponse.Contains("errors"))
                {
                    // Pokud ano, zobrazíme uživateli celou chybovou zprávu od GraphQL.
                    await Shell.Current.DisplayAlert("Chyba od GraphQL", jsonResponse, "OK");
                }
                else
                {
                    // Pokud je vše v pořádku, zobrazíme uživateli potvrzení o úspěchu.
                    await Shell.Current.DisplayAlert("Odesláno", "Příkaz byl úspěšně zpracován serverem.", "OK");
                }
            }
            else
            {
                // Pokud HTTP přenos selhal (např. chyba 404, 500), zobrazíme stavový kód a obsah chyby.
                var errorContent = await response.Content.ReadAsStringAsync();
                await Shell.Current.DisplayAlert("Chyba serveru", $"Chyba: {response.StatusCode}\n{errorContent}", "OK");
            }
        }
        catch (Exception ex)
        {
            // Pokud dojde k jakékoliv jiné výjimce (např. zařízení je offline), odchytíme ji zde.
            await Shell.Current.DisplayAlert("Chyba aplikace", ex.Message, "OK");
        }
        finally
        {
            // Blok "finally" se provede vždy, ať už operace proběhla úspěšně, nebo selhala.
            // Zajišťuje, že příznak "zaneprázdněno" se vždy vrátí na false a UI se odblokuje.
            IsBusy = false;
        }
    }

    /// <summary>
    /// Pomocná metoda pro načtení, deserializaci a zobrazení zpráv.
    /// Zajišťuje, že aktualizace kolekce Messages proběhne bezpečně na hlavním UI vlákně.
    /// </summary>
    private async Task FetchAndDisplayMessagesAsync()
    {
        var request = new { query = GetAllMessagesQuery };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(ApiUrl, content);

        if (response.IsSuccessStatusCode)
        {
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var messagesResponse = JsonSerializer.Deserialize<GraphQLMessagesResponse>(jsonResponse);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Messages.Clear();
                if (messagesResponse?.Data?.FreeMessages != null)
                {
                    var sortedMessages = messagesResponse.Data.FreeMessages.OrderByDescending(m => m.CreatedUtc);
                    foreach (var message in sortedMessages)
                    {
                        Messages.Add(message);
                    }
                }
            });
        }
    }
}