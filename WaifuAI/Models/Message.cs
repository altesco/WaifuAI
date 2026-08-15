using System;
using System.Text.Json.Serialization;
using SQLite;

namespace WaifuAI.Models;

public class Message
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonIgnore] public string CleanText { get; set; } = string.Empty;

    [PrimaryKey]
    [JsonIgnore] public Guid Id { get; set; } = Guid.NewGuid();

    [JsonIgnore] public DateTime Time { get; set; }
    [JsonIgnore] public int Tokens { get; set; }
    [JsonIgnore] public string? Quote { get; set; }
    [JsonIgnore] public int QuoteStart { get; set; }
    [JsonIgnore] public int QuoteEnd { get; set; }
    [JsonIgnore] public bool? IsReplied { get; set; }
    [JsonIgnore] public double DesignHeight { get; set; }

    [Indexed]
    [JsonIgnore] public Guid? ReplyMessageId { get; set; }
}