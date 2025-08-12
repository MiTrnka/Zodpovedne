using System.Text;
using System.Text.Json; // Potřeba pro formátování JSONu pro lepší čitelnost

namespace Zodpovedne.MAUIApp;

public partial class MainPage : ContentPage
{
    // Vytvoříme si jednu instanci HttpClient, kterou můžeme používat opakovaně
    private readonly HttpClient client = new HttpClient();

    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnCallApiButtonClicked(object sender, EventArgs e)
    {
        // Zobrazíme uživateli, že se něco děje
        ResultLabel.Text = "Načítám data...";

        try
        {
            // 1. Adresa vašeho GraphQL API
            var apiUrl = "https://api.discussion.cz/graphql";

            // 2. Váš GraphQL dotaz
            // Pozor na uvozovky uvnitř uvozovek. V C# je řešíme zpětným lomítkem (\").
            var graphQLQuery = @"{
                ""query"": ""query NactiMichalovyZpravy { freeMessages(where: { nickname: { eq: \""Markéta\"" } }) { id text } }""
            }";

            // 3. Vytvoření obsahu HTTP požadavku (payload)
            var content = new StringContent(graphQLQuery, Encoding.UTF8, "application/json");

            // 4. Odeslání POST požadavku na API
            HttpResponseMessage response = await client.PostAsync(apiUrl, content);

            // 5. Zpracování odpovědi
            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();

                // Pro hezčí zobrazení můžeme JSON naformátovat
                // Pokud nemáte System.Text.Json, můžete tento krok přeskočit a zobrazit jen "jsonResponse"
                var options = new JsonSerializerOptions { WriteIndented = true };
                var formattedJson = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(jsonResponse), options);

                ResultLabel.Text = formattedJson; // Zobrazíme výsledek
            }
            else
            {
                ResultLabel.Text = $"Chyba: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            // Pokud nastane jakákoliv chyba (např. není internet), zobrazíme ji
            ResultLabel.Text = $"Výjimka: {ex.Message}";
        }
    }
}