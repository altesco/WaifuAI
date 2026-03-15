using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WaifuAI.Models;

public class QueryModel
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "any";

    [JsonPropertyName("messages")]
    public List<Message> Messages { get; set; } = [];

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.7;

    [JsonPropertyName("extra_body")] 
    public ExtraBody ExtraBody { get; set; } = new();
}

public class ExtraBody
{
    [JsonPropertyName("chat_template_kwargs")]
    public ChatTemplateKwargs ChatTemplateKwargs { get; set; } = new();

    [JsonPropertyName("enable_thinking")]
    public bool EnableThinking { get; set; } = false;
}

public class ChatTemplateKwargs
{
    [JsonPropertyName("enable_thinking")]
    public bool EnableThinking { get; set; } = false;
}