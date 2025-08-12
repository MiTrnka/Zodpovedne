using System.Text.Json.Serialization;

namespace Zodpovedne.MAUIApp.GraphQL.Models;

// Model pro tělo HTTP POST požadavku
public class GraphQLRequest
{
    [JsonPropertyName("query")]
    public string Query { get; set; }

    [JsonPropertyName("variables")]
    public object? Variables { get; set; }
}