// --- Potřebné using direktivy ---
using CommunityToolkit.Mvvm.ComponentModel; // Základní stavební kameny pro MVVM, jako je ObservableObject.
using CommunityToolkit.Mvvm.Input;         // Atribut [RelayCommand] pro snadné vytváření příkazů.
using System.Collections.ObjectModel;     // ObservableCollection, která umí informovat UI o změnách.
using System.Linq;                        // Metody pro práci s kolekcemi, zde konkrétně OrderByDescending.
using System.Text;                        // Práce s kódováním textu (UTF8).
using System.Text.Json;                   // Serializace a deserializace C# objektů na/z JSON.
using Zodpovedne.MAUIApp.Models;           // Přístup k našim datovým modelům (FreeMessage).

namespace Zodpovedne.MAUIApp.ViewModels;

/// <summary>
/// Třída ViewModelu pro hlavní stránku chatu.
/// Tato třída funguje jako "mozek" stránky - obsahuje data (vlastnosti) a logiku (příkazy/metody),
/// které jsou oddělené od samotného uživatelského rozhraní (XAML).
/// Dědí z 'ObservableObject', což jí umožňuje posílat notifikace do UI o změnách hodnot jejích vlastností.
/// Klíčové slovo 'partial' je zde nutné, protože CommunityToolkit.Mvvm na pozadí generuje další části této třídy.
/// </summary>
public partial class ChatViewModel : ObservableObject
{
    // --- Soukromá pole a konstanty ---

    /// <summary>
    /// Instance pro komunikaci přes HTTP protokol. Je 'static readonly', což znamená,
    /// že v celé aplikaci existuje pouze jedna sdílená instance, která se po vytvoření nemění.
    /// Pro jednoduchou aplikaci je to dostačující přístup.
    /// </summary>
    private static readonly HttpClient client = new();

    /// <summary>
    /// URL adresa GraphQL API serveru. Jako konstanta je zde pro snadnou změnu na jednom místě.
    /// </summary>
    private const string ApiUrl = "https://api.discussion.cz/graphql";

    /// <summary>
    /// Text GraphQL dotazu pro načtení všech zpráv. Uložení dotazu do konstanty zpřehledňuje kód v metodách.
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
    /// Šablona GraphQL mutace pro odeslání nové zprávy. Používá zástupné symboly {0} a {1},
    /// které budou později nahrazeny skutečnými hodnotami (přezdívkou a textem).
    /// </summary>
    private const string CreateMessageMutationTemplate = @"
        mutation CreateMessage {{
            addFreeMessage(input: {{ nickname: ""{0}"", text: ""{1}"" }}) {{
                id
            }}
        }}";

    // --- Veřejné vlastnosti (Properties) propojené s UI ---

    /// <summary>
    /// Atribut [ObservableProperty] je mocný nástroj z CommunityToolkit.Mvvm.
    /// Automaticky za nás na pozadí vygeneruje kompletní veřejnou vlastnost (např. 'public bool IsBusy { get; set; }')
    /// a zároveň zajistí, že při každé změně její hodnoty se automaticky zavolá notifikace pro UI.
    /// Tento atribut funguje POUZE pokud je třída označena jako 'partial'.
    /// </summary>
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string nickname;

    [ObservableProperty]
    private string messageText;

    /// <summary>
    /// Speciální typ kolekce, která je navržena pro práci s UI.
    /// Pokud do této kolekce přidáte nebo z ní odeberete položku,
    /// prvek v XAML, který je na ni napojen (např. CollectionView), se automaticky sám aktualizuje.
    /// </summary>
    public ObservableCollection<FreeMessage> Messages { get; } = new();

    // --- Konstruktor ---

    /// <summary>
    /// Konstruktor je metoda, která se zavolá automaticky při vytvoření nové instance ViewModelu.
    /// Je to ideální místo pro prvotní inicializaci.
    /// </summary>
    public ChatViewModel()
    {
        // Hned na začátku spustíme příkaz pro načtení zpráv, aby uživatel viděl data co nejdříve.
        LoadMessagesCommand.Execute(null);
    }

    // --- Příkazy (Commands) propojené s UI ---

    /// <summary>
    /// Atribut [RelayCommand] přemění soukromou metodu 'LoadMessagesAsync' na veřejný, bindovatelný
    /// příkaz s názvem 'LoadMessagesCommand'. Na tento příkaz můžeme v XAMLu napojit např. tlačítko.
    /// Tento příkaz slouží jako veřejný "obal" pro skutečnou logiku, který spravuje stav 'IsBusy'.
    /// </summary>
    [RelayCommand]
    private async Task LoadMessagesAsync()
    {
        // Zabráníme spuštění, pokud už nějaká operace (načítání/odesílání) běží.
        if (IsBusy) return;

        IsBusy = true; // Označíme, že operace začala.
        try
        {
            // Zavoláme soukromou metodu, která obsahuje skutečnou logiku načítání.
            await FetchAndDisplayMessagesAsync();
        }
        catch (Exception ex)
        {
            // Pokud během procesu nastane jakákoliv chyba, zobrazíme ji uživateli.
            await Shell.Current.DisplayAlert("Chyba načítání", ex.Message, "OK");
        }
        finally
        {
            // Tento blok se vykoná VŽDY a zajistí, že vždy vypneme indikátor aktivity.
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

        // Validace vstupu na straně klienta.
        if (string.IsNullOrWhiteSpace(Nickname) || string.IsNullOrWhiteSpace(MessageText))
        {
            await Shell.Current.DisplayAlert("Chyba", "Musíte vyplnit přezdívku i text zprávy.", "OK");
            return; // Ukončíme metodu, pokud validace selže.
        }

        IsBusy = true;
        try
        {
            // Pokud by přezdívka nebo text obsahovaly uvozovky, rozbilo by to strukturu našeho
            // textového GraphQL dotazu. Proto je "escapujeme" (před uvozovku přidáme zpětné lomítko).
            var escapedNickname = Nickname.Replace("\"", "\\\"");
            var escapedText = MessageText.Replace("\"", "\\\"");

            // Sestavíme finální dotaz vložením escapovaných hodnot do naší šablony.
            var query = string.Format(CreateMessageMutationTemplate, escapedNickname, escapedText);

            var request = new { query };
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(ApiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                if (responseBody.Contains("errors"))
                {
                    await Shell.Current.DisplayAlert("Chyba GraphQL", responseBody, "OK");
                }
                else
                {
                    // Pokud vše proběhlo v pořádku:
                    MessageText = string.Empty; // Vyčistíme pole pro zprávu v UI.

                    // OPRAVA: Místo volání celého příkazu LoadMessagesAsync (který by se kvůli 'IsBusy' nespustil),
                    // zavoláme přímo soukromou metodu, která provede načtení a aktualizaci UI.
                    await FetchAndDisplayMessagesAsync();
                }
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
    /// Soukromá pomocná metoda, která obsahuje čistou logiku pro načtení zpráv z API a jejich zobrazení.
    /// Nemá v sobě žádnou správu stavu 'IsBusy', proto ji můžeme bezpečně volat z jiných metod.
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

            Messages.Clear(); // Vyčistíme kolekci od starých zpráv.
            if (messagesResponse?.Data?.FreeMessages != null)
            {
                // Seřadíme zprávy sestupně podle data vytvoření, aby nejnovější byly nahoře.
                var sortedMessages = messagesResponse.Data.FreeMessages.OrderByDescending(m => m.CreatedUtc);
                foreach (var message in sortedMessages)
                {
                    Messages.Add(message); // Přidáme zprávy do kolekce, což automaticky aktualizuje UI.
                }
            }
        }
    }
}