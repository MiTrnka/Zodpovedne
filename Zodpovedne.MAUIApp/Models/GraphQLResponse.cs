using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Zodpovedne.MAUIApp.GraphQL.Models;

// Obecná struktura odpovědi pro Query (GetMessages)
public class GraphQLResponse<T>
{
    [JsonPropertyName("data")]
    public T Data { get; set; }
}

public class FreeMessagesData
{
    [JsonPropertyName("freeMessages")]
    public FreeMessagesConnection FreeMessages { get; set; }
}

public class FreeMessagesConnection
{
    [JsonPropertyName("nodes")]
    public List<FreeMessage> Nodes { get; set; }
}

// Model pro samotnou zprávu (musí odpovídat polím v GraphQL dotazu)
public class FreeMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("createdUtc")]
    public DateTime CreatedUtc { get; set; }
}