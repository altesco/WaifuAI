using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WaifuAI.Models;

public class QueryModel
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "any";

    [JsonPropertyName("messages")]
    public List<Message> Messages { get; set; } = [];

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.7;
}