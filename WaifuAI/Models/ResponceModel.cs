using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WaifuAI.Models
{
    internal class ResponceModel
    {
        [JsonPropertyName("choices")]
        public List<Choice> Choices { get; set; } = [];
    }

    internal class Choice
    {
        [JsonPropertyName("message")]
        public Message Message { get; set; } = new();

        [JsonPropertyName("finish_reason")]
        public string FinishReason { get; set; } = string.Empty;
    }
}
