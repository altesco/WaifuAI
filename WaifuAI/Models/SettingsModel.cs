using System;

namespace WaifuAI.Models;

public class SettingsModel
{
    // AI Source
    public int Port { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string AIModel { get; set; } = string.Empty;
    public bool IsServerQuery { get; set; }

    // AI Parameters
    public double Temperature { get; set; } = 0.7;
    public ResponseLength ResponseLength { get; set; } = ResponseLength.Medium;
    public int MaxTokens { get; set; } = 1000;
    public int ContextLength { get; set; } = 120000;

    // General Settings
    public int Theme { get; set; }
    public string AccentColor { get; set; } = "#4287f5";
    public string Font { get; set; } = string.Empty;
    public string MonospaceFont { get; set; } = string.Empty;
    public string AppLanguage { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;

    // Sound Settings
    public string Source { get; set; } = "silero_tts";
    public string VoiceModel { get; set; } = "v3_en";
    public string Speaker { get; set; } = string.Empty;
    public double Volume { get; set; } = 1.0;
    public double Bass { get; set; }
    public double Treble { get; set; }
    public double Pitch { get; set; } = 1.0;
    public bool IsStream { get; set; }

    // 3D Model Settings
    public string SelectedModel3D { get; set; } = string.Empty;
    public string Model3DFolder { get; set; } = string.Empty;

    // Personality Settings
    public string WaifuName { get; set; } = "Waifu";
    public DateOnly Birthday { get; set; } = new(2000, 01, 01);
    public string SelectedArchetype { get; set; } = string.Empty;

    // Emotional State System
    public int Affection { get; set; } = 50;
    public int Engagement { get; set; } = 50;
    public int Mood { get; set; } = 50;
    public int Energy { get; set; } = 50;
}