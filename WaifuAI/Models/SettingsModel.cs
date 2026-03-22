using System.Formats.Asn1;

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

    // General Settings
    public int Theme { get; set; }
    public string AppLanguage { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;

    // Sound Settings
    public string Source { get; set; } = string.Empty;
    public string VoiceModel { get; set; } = string.Empty;
    public string Speaker { get; set; } = string.Empty;
    public double Volume { get; set; } = 1.0;
    public double Bass { get; set; }
    public double Treble { get; set; }
    public double Pitch { get; set; } = 1.0;

    // 3D Model Settings
    public string SelectedModel3D { get; set; } = string.Empty;
    public string Model3DFolder { get; set; } = string.Empty;
}