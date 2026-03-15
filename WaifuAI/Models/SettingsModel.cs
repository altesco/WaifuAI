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

    // Sound Settings
    public string Source { get; set; } = string.Empty;
    public string VoiceModel { get; set; } = string.Empty;
    public string Speaker { get; set; } = string.Empty;
    public double Volume { get; set; }
    public double Bass { get; set; }
    public double Treble { get; set; }

}