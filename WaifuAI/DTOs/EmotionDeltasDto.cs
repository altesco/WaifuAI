using System.Text.Json.Serialization;

namespace WaifuAI.DTOs;

public class EmotionDeltasDto
{
    [JsonPropertyName("affection_delta")] 
    public int AffectionDelta { get; set; }

    [JsonPropertyName("engagement_delta")]
    public int EngagementDelta { get; set; }

    [JsonPropertyName("mood_delta")]
    public int MoodDelta { get; set; }

    [JsonPropertyName("energy_delta")]
    public int EnergyDelta { get; set; }
}

