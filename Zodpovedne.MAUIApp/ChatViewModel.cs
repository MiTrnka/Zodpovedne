// --- Potřebné using direktivy ---
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plugin.Firebase.CloudMessaging;      // Pro práci s Firebase notifikacemi
using System.Collections.ObjectModel;     // ObservableCollection
using System.Linq;                        // Metoda OrderByDescending
using System.Text;                        // Práce s kódováním textu (UTF8)
using System.Text.Json;                   // Serializace a deserializace JSON
using Zodpovedne.MAUIApp.Models;           // Přístup k datovým modelům (FreeMessage)

namespace Zodpovedne.MAUIApp.ViewModels;

/// <summary>
/// ViewModel pro hlavní stránku chatu. Funguje jako "mozek" stránky.
/// Dědí z 'ObservableObject', což mu umožňuje informovat UI o změnách vlastností.
/// Je 'partial', aby CommunityToolkit.Mvvm mohl na pozadí generovat kód.
/// </summary>
public partial class ChatViewModel : ObservableObject
{
    // --- Soukromá pole a konstanty ---

    private static readonly HttpClient client = new();
    private const string ApiUrl = "https://api.discussion.cz/graphql";

    /// <summary>
    /// Služba pro práci s notifikacemi, kterou získáme pomocí Dependency Injection.
    /// Je 'readonly', takže ji lze nastavit pouze v konstruktoru.
    /// </summary>
    private readonly IFirebaseCloudMessaging _firebaseCloudMessaging;

    private const string GetAllMessagesQuery = @"
        query NactiVsechnyZpravy {
            freeMessages {
                id
                nickname
                text
                createdUtc
            }
        }";

    private const string CreateMessageMutationTemplate = @"
        mutation CreateMessage {{
            addFreeMessage(input: {{ nickname: ""{0}"", text: ""{1}"" }}) {{
                id
            }}
        }}";

    /// <summary>
    /// Šablona GraphQL mutace pro registraci FCM tokenu na našem serveru.
    /// </summary>
    private const string RegisterFcmTokenMutationTemplate = @"
        mutation RegisterFcmToken {{
            registerFcmToken(token: ""{0}"")
        }}";


    // --- Veřejné vlastnosti (Properties) propojené s UI ---

    /// <summary>
    /// Atribut [ObservableProperty] automaticky generuje veřejnou vlastnost (property)
    /// a zajišťuje notifikace pro UI při její změně.
    /// </summary>
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string nickname;

    [ObservableProperty]
    private string messageText;

    /// <summary>
    /// Kolekce zpráv, která automaticky aktualizuje UI při přidání/odebrání položky.
    /// </summary>
    public ObservableCollection<FreeMessage> Messages { get; } = new();


    // --- Konstruktor ---

    /// <summary>
    /// Konstruktor se zavolá při vytvoření instance ViewModelu.
    /// Díky Dependency Injection nám MAUI automaticky předá instanci IFirebaseCloudMessaging.
    /// </summary>
    public ChatViewModel(IFirebaseCloudMessaging firebaseCloudMessaging)
    {
        // Uložíme si předanou službu pro pozdější použití.
        _firebaseCloudMessaging = firebaseCloudMessaging;

        // Spustíme inicializační logiku na pozadí, abychom neblokovali UI.
        Task.Run(InitializeAsync);
    }

    /// <summary>
    /// Provádí asynchronní inicializaci ViewModelu.
    /// </summary>
    private async Task InitializeAsync()
    {
        // Spustíme načtení zpráv a registraci pro notifikace.
        // Není třeba na dokončení čekat, poběží na pozadí.
        await LoadMessagesCommand.ExecuteAsync(null);
        await RegisterForNotificationsCommand.ExecuteAsync(null);
    }


    // --- Příkazy (Commands) propojené s UI ---

    /// <summary>
    /// Příkaz pro načtení zpráv ze serveru.
    /// Atribut [RelayCommand] z něj vytvoří bindovatelný ICommand s názvem 'LoadMessagesCommand'.
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
    /// Příkaz pro odeslání nové zprávy.
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
            var escapedNickname = Nickname.Replace("\"", "\\\"");
            var escapedText = MessageText.Replace("\"", "\\\"");
            var query = string.Format(CreateMessageMutationTemplate, escapedNickname, escapedText);

            var request = new { query };
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(ApiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                MessageText = string.Empty;
                // Po úspěšném odeslání znovu načteme všechny zprávy.
                await FetchAndDisplayMessagesAsync();
            }
            else
            {
                await Shell.Current.DisplayAlert("Chyba API", await response.Content.ReadAsStringAsync(), "OK");
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
    /// Příkaz, který se postará o získání FCM tokenu a jeho odeslání na náš backend.
    /// </summary>
    [RelayCommand]
    private async Task RegisterForNotificationsAsync()
    {
        try
        {
            // Požádáme o oprávnění zobrazovat notifikace (nutné pro novější verze Androidu).
            await _firebaseCloudMessaging.CheckIfValidAsync();

            // Získáme unikátní FCM token pro toto konkrétní zařízení.
            var token = await _firebaseCloudMessaging.GetTokenAsync();

            if (string.IsNullOrEmpty(token))
            {
                // Pokud se z nějakého důvodu token nepodařilo získat, dál nepokračujeme.
                await Shell.Current.DisplayAlert("Chyba Firebase", "Nepodařilo se získat FCM token.", "OK");
                return;
            }

            // Sestavíme a odešleme GraphQL mutaci pro registraci tokenu.
            var query = string.Format(RegisterFcmTokenMutationTemplate, token);
            var request = new { query };
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(ApiUrl, content);

            // Pokud server vrátí chybu, zobrazíme ji. Jinak v tichosti předpokládáme úspěch.
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                await Shell.Current.DisplayAlert("Chyba registrace tokenu", error, "OK");
            }
        }
        catch (Exception ex)
        {
            // Zobrazíme jakoukoliv jinou výjimku, která by mohla nastat.
            await Shell.Current.DisplayAlert("Výjimka při registraci", ex.Message, "OK");
        }
    }

    // --- Soukromé pomocné metody ---

    /// <summary>
    /// Pomocná metoda, která obsahuje čistou logiku pro načtení a zobrazení zpráv.
    /// Lze ji bezpečně volat z více míst v kódu.
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

            Messages.Clear();
            if (messagesResponse?.Data?.FreeMessages != null)
            {
                var sortedMessages = messagesResponse.Data.FreeMessages.OrderByDescending(m => m.CreatedUtc);
                foreach (var message in sortedMessages)
                {
                    Messages.Add(message);
                }
            }
        }
    }
}