using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Zodpovedne.MAUIApp.GraphQL.Models;

namespace Zodpovedne.MAUIApp.Services;

public class GraphQLService
{
    private readonly HttpClient _httpClient;
    private ClientWebSocket? _clientWebSocket;
    private readonly Uri _httpEndpoint;
    private readonly Uri _webSocketEndpoint;

    // Událost, která se spustí, když přijde nová zpráva přes WebSocket
    public event Action<FreeMessage>? OnMessageReceived;

    public GraphQLService()
    {
#if DEBUG
#if ANDROID
        // Pro Android emulátor použijeme speciální IP 10.0.2.2 pro přístup
        // k 'localhost' na hostitelském počítači.
        const string graphqlEndpoint = "https://10.0.2.2:7206/graphql";
#else
        // Pro ostatní platformy (Windows, iOS simulátor) je 'localhost' v pořádku.
        const string graphqlEndpoint = "https://localhost:7206/graphql";
#endif
#else
    // Produkční URL zůstává beze změny.
    const string graphqlEndpoint = "https://api.discussion.cz/graphql";
#endif

        _httpEndpoint = new Uri(graphqlEndpoint);
        _webSocketEndpoint = new Uri(graphqlEndpoint.Replace("https", "wss"));

        // Tato část je teď ještě důležitější!
        // Spojení na 10.0.2.2 bude používat lokální 'localhost' certifikát, kterému
        // Android systém nedůvěřuje. Tento kód zajistí, že se kontrola certifikátu
        // pro ladění přeskočí a spojení se podaří navázat.
#if DEBUG && ANDROID
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        _httpClient = new HttpClient(handler);
#else
    _httpClient = new HttpClient();
#endif
    }
    // --- QUERY ---
    public async Task<List<FreeMessage>> GetMessagesAsync()
    {
        var graphQLQuery = @"
            query GetMessages {
              freeMessages(order: { createdUtc: DESC }) {
                nodes {
                  id
                  nickname
                  text
                  createdUtc
                }
              }
            }";

        var request = new GraphQLRequest { Query = graphQLQuery };
        var response = await _httpClient.PostAsJsonAsync(_httpEndpoint, request);

        if (!response.IsSuccessStatusCode) return new List<FreeMessage>();

        var graphQLResponse = await response.Content.ReadFromJsonAsync<GraphQLResponse<FreeMessagesData>>();
        return graphQLResponse?.Data?.FreeMessages?.Nodes ?? new List<FreeMessage>();
    }

    // --- MUTATION ---
    public async Task SendMessageAsync(string nickname, string text)
    {
        var graphQLMutation = @"
            mutation SendMessage($input: AddFreeMessageInput!) {
              addFreeMessage(input: $input) {
                id
              }
            }";

        var request = new GraphQLRequest
        {
            Query = graphQLMutation,
            Variables = new { input = new { nickname, text } }
        };
        await _httpClient.PostAsJsonAsync(_httpEndpoint, request);
    }

    // --- SUBSCRIPTION ---
    public async Task ConnectAndSubscribeAsync(CancellationToken cancellationToken)
    {
        if (_clientWebSocket?.State == WebSocketState.Open) return;

        _clientWebSocket = new ClientWebSocket();
        _clientWebSocket.Options.AddSubProtocol("graphql-ws");

        await _clientWebSocket.ConnectAsync(_webSocketEndpoint, cancellationToken);

        // 1. Handshake
        var initMessage = new { type = "connection_init" };
        await SendWebSocketMessageAsync(JsonSerializer.Serialize(initMessage), cancellationToken);

        // 2. Start Subscription
        var subscriptionQuery = @"
        subscription OnNewMessage {
          onFreeMessageAdded {
            id
            nickname
            text
            createdUtc
          }
        }";
        var subscribeMessage = new
        {
            id = "1",
            type = "start",
            payload = new { query = subscriptionQuery }
        };
        await SendWebSocketMessageAsync(JsonSerializer.Serialize(subscribeMessage), cancellationToken);

        // 3. Start Listening Loop - ZDE JE KLÍČOVÁ ZMĚNA
        // Spustíme naslouchání, ale nečekáme na jeho dokončení (`await`).
        // Zápis `_ = ` dává kompilátoru najevo, že je to záměr ("fire and forget").
        // Metoda ConnectAndSubscribeAsync se tak okamžitě vrátí a neblokuje UI.
        _ = ListenForMessagesAsync(cancellationToken);
    }

    private async Task ListenForMessagesAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 4];
        while (_clientWebSocket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) break;

            var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var messageDoc = JsonDocument.Parse(messageJson);

            if (messageDoc.RootElement.TryGetProperty("type", out var typeElement))
            {
                // Zpracováváme jen datové zprávy
                if (typeElement.GetString() == "data")
                {
                    var payload = messageDoc.RootElement.GetProperty("payload");
                    var data = payload.GetProperty("data");
                    var message = data.GetProperty("onFreeMessageAdded").Deserialize<FreeMessage>();

                    // Spustíme událost a předáme novou zprávu
                    if (message != null)
                    {
                        OnMessageReceived?.Invoke(message);
                    }
                }
            }
        }
    }

    private Task SendWebSocketMessageAsync(string message, CancellationToken cancellationToken)
    {
        var messageBuffer = Encoding.UTF8.GetBytes(message);
        return _clientWebSocket!.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, cancellationToken);
    }

    public async Task DisconnectAsync()
    {
        if (_clientWebSocket?.State == WebSocketState.Open)
        {
            await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        }
    }
}