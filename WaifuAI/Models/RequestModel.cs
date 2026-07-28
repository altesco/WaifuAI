using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WaifuAI.Models;

public class RequestModel
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "any";

    [JsonPropertyName("messages")]
    public List<Message> Messages { get; set; } = [];

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }
}