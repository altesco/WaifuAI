namespace WaifuAI.Models;

public class SettingsModel
{
    public int Port { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}