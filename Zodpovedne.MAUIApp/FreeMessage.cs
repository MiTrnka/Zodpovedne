using System.Text.Json.Serialization;

namespace Zodpovedne.MAUIApp.Models
{
    /// <summary>
    /// Tento záznam (record) reprezentuje jednu zprávu.
    /// Typy vlastností nyní přesně odpovídají datovému modelu na serveru.
    /// </summary>
    public record FreeMessage
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("nickname")]
        public string Nickname { get; init; }

        [JsonPropertyName("text")]
        public string Text { get; init; }

        [JsonPropertyName("createdUtc")]
        public DateTime CreatedUtc { get; init; }
    }

    // Pomocné třídy pro deserializaci zůstávají stejné.
    public record GraphQLMessagesResponse
    {
        [JsonPropertyName("data")]
        public MessagesData Data { get; init; }
    }

    public record MessagesData
    {
        [JsonPropertyName("freeMessages")]
        public List<FreeMessage> FreeMessages { get; init; }
    }
}