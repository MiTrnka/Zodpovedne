using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Zodpovedne.MAUIApp.GraphQL.Models; // Náš model pro zprávu
using Zodpovedne.MAUIApp.Services;     // Naše služba

namespace Zodpovedne.MAUIApp.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly GraphQLService _graphQLService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _nickname = "Anonym";

    [ObservableProperty]
    private string _newMessageText = "";

    public ObservableCollection<FreeMessage> Messages { get; } = new();

    public ChatViewModel(GraphQLService graphQLService)
    {
        _graphQLService = graphQLService;
        // Přihlásíme se k odběru události z naší služby
        _graphQLService.OnMessageReceived += OnNewMessageReceived;
    }

    private void OnNewMessageReceived(FreeMessage message)
    {
        // UI se musí aktualizovat na hlavním vlákně
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Messages.Insert(0, message);
        });
    }

    public async Task InitializeAsync()
    {
        // 1. Načteme existující zprávy
        var messages = await _graphQLService.GetMessagesAsync();
        foreach (var message in messages)
        {
            Messages.Add(message);
        }

        // 2. Spustíme odběr na nové zprávy
        _cancellationTokenSource = new CancellationTokenSource();
        await _graphQLService.ConnectAndSubscribeAsync(_cancellationTokenSource.Token);
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(NewMessageText)) return;
        await _graphQLService.SendMessageAsync(Nickname, NewMessageText);
        NewMessageText = "";
    }

    public async Task CleanupAsync()
    {
        _cancellationTokenSource?.Cancel(); // Zastaví naslouchací smyčku
        await _graphQLService.DisconnectAsync();
        _graphQLService.OnMessageReceived -= OnNewMessageReceived;
    }
}