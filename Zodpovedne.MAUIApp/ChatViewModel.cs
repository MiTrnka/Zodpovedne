using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Linq; // Potřeba pro OrderByDescending
using Zodpovedne.MAUIApp.Models;

namespace Zodpovedne.MAUIApp.ViewModels
{
    public partial class ChatViewModel : ObservableObject
    {
        private static readonly HttpClient client = new HttpClient();
        private readonly string apiUrl = "https://api.discussion.cz/graphql";

        [ObservableProperty]
        private string nickname;

        [ObservableProperty]
        private string messageText;

        public ObservableCollection<FreeMessage> Messages { get; } = new();

        public ChatViewModel()
        {
            // Na začátku načteme existující zprávy.
            LoadMessagesCommand.Execute(null);
        }

        /// <summary>
        /// Načte všechny zprávy z API
        /// </summary>
        [RelayCommand]
        private async Task LoadMessagesAsync()
        {
            try
            {
                // POUŽÍVÁME VÁŠ DOTAZ: query NactiVsechnyZpravy { ... }
                var query = @"
                    query NactiVsechnyZpravy {
                        freeMessages {
                            id
                            nickname
                            text
                            createdUtc
                        }
                    }";

                var request = new { query }; // Vytvoříme JSON objekt { "query": "..." }
                var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var messagesResponse = JsonSerializer.Deserialize<GraphQLMessagesResponse>(jsonResponse);

                    Messages.Clear();
                    if (messagesResponse?.Data?.FreeMessages != null)
                    {
                        // Seřadíme zprávy od nejnovější po nejstarší podle pole 'createdUtc'
                        var sortedMessages = messagesResponse.Data.FreeMessages.OrderByDescending(m => m.CreatedUtc);
                        foreach (var message in sortedMessages)
                        {
                            Messages.Add(message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Chyba načítání", ex.Message, "OK");
            }
        }

        /// <summary>
        /// Odešle novou zprávu
        /// Sestavuje dotaz jako textový řetězec.
        /// </summary>
        [RelayCommand]
        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(Nickname) || string.IsNullOrWhiteSpace(MessageText))
            {
                await Shell.Current.DisplayAlert("Chyba", "Musíte vyplnit přezdívku i text zprávy.", "OK");
                return;
            }

            try
            {
                // Zabezpečení proti uvozovkám v textu. Nahradí " za \"
                var escapedNickname = Nickname.Replace("\"", "\\\"");
                var escapedText = MessageText.Replace("\"", "\\\"");

                // POUŽÍVÁME VAŠI MUTACI: Sestavíme ji dynamicky pomocí C# string interpolace.
                // Výsledkem bude přesně ten text, který jste poslal.
                var query = $@"
                    mutation CreateMessage {{
                        addFreeMessage(input: {{ nickname: ""{escapedNickname}"", text: ""{escapedText}"" }}) {{
                            id
                        }}
                    }}";

                var request = new { query };
                var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    if (responseBody.Contains("errors"))
                    {
                        await Shell.Current.DisplayAlert("Chyba GraphQL", responseBody, "OK");
                        return;
                    }

                    // Po úspěšném odeslání vyčistíme pole a znovu načteme zprávy.
                    MessageText = string.Empty;
                    await LoadMessagesAsync();
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
        }
    }
}