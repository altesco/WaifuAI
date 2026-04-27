using System;
using System.Text.Json.Serialization;

namespace WaifuAI.Models;

public class Message
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonIgnore] public Guid Id { get; set; } = Guid.NewGuid();
    [JsonIgnore] public DateTime Time { get; set; }
    [JsonIgnore] public int Tokens { get; set; }
}