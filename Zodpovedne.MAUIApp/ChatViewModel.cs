using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plugin.Firebase.CloudMessaging;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using Zodpovedne.MAUIApp.Models;

namespace Zodpovedne.MAUIApp.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private static readonly HttpClient client = new();
    private const string ApiUrl = "https://api.discussion.cz/graphql";
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

    private const string RegisterFcmTokenMutationTemplate = @"
        mutation RegisterFcmToken {{
            registerFcmToken(token: ""{0}"")
        }}";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string nickname;

    [ObservableProperty]
    private string messageText;

    public ObservableCollection<FreeMessage> Messages { get; } = new();

    public ChatViewModel(IFirebaseCloudMessaging firebaseCloudMessaging)
    {
        _firebaseCloudMessaging = firebaseCloudMessaging;
        Task.Run(InitializeAsync);
    }

    private async Task InitializeAsync()
    {
        await LoadMessagesCommand.ExecuteAsync(null);
        await RegisterForNotificationsCommand.ExecuteAsync(null);
    }

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

    [RelayCommand]
    private async Task RegisterForNotificationsAsync()
    {
        try
        {
            await _firebaseCloudMessaging.CheckIfValidAsync();
            var token = await _firebaseCloudMessaging.GetTokenAsync();

            if (string.IsNullOrEmpty(token))
            {
                await Shell.Current.DisplayAlert("Chyba Firebase", "Nepodařilo se získat FCM token.", "OK");
                return;
            }

            var query = string.Format(RegisterFcmTokenMutationTemplate, token);
            var request = new { query };
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(ApiUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                await Shell.Current.DisplayAlert("Chyba registrace tokenu", error, "OK");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Výjimka při registraci", ex.Message, "OK");
        }
    }

    private async Task FetchAndDisplayMessagesAsync()
    {
        var request = new { query = GetAllMessagesQuery };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(ApiUrl, content);

        if (response.IsSuccessStatusCode)
        {
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var messagesResponse = JsonSerializer.Deserialize<GraphQLMessagesResponse>(jsonResponse);

            // Změny v kolekci "posíláme" na hlavní vlákno, aby nedošlo ke konfliktu
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Messages.Clear(); // Teď je to bezpečné
                if (messagesResponse?.Data?.FreeMessages != null)
                {
                    var sortedMessages = messagesResponse.Data.FreeMessages.OrderByDescending(m => m.CreatedUtc);
                    foreach (var message in sortedMessages)
                    {
                        Messages.Add(message); // A toto také
                    }
                }
            });
        }
    }
}