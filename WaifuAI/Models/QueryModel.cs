using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WaifuAI.Models
{
    internal class QueryModel
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "gemini-2.5-flash";//"any";

        [JsonPropertyName("messages")]
        public List<Message> Messages { get; set; } = [];

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.7; 
    }
}