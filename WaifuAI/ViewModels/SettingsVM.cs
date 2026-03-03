using System;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaifuAI.Models;

namespace WaifuAI.ViewModels;

public partial class SettingsVM : ObservableObject
{
    private SettingsVM() { }
    
    private static SettingsVM? _instance;
    public static SettingsVM Instance => _instance ??= new SettingsVM();
    
    private static readonly string AppDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "WaifuAI"
    );

    private static readonly string FilePath = Path.Combine(AppDirectory, "settings.json");

    [ObservableProperty] private bool _isServerQuery;
    [ObservableProperty] private int _port;
    [ObservableProperty] private string _ipAddress;
    [ObservableProperty] private string _apiKey;
    [ObservableProperty] private string _apiUrl;
    [ObservableProperty] private string _model;
    private SettingsModel SettingsModel { get; set; }

    public void Load()
    {
        if (!File.Exists(FilePath))
        {
            SettingsModel = new SettingsModel();
            return;
        }
        var json = File.ReadAllText(FilePath);
        SettingsModel = JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();
        Port = SettingsModel.Port;
        IpAddress = SettingsModel.IpAddress;
        ApiKey = SettingsModel.ApiKey;
        ApiUrl = SettingsModel.ApiUrl;
        Model = SettingsModel.Model;
    }

    [RelayCommand]
    private void Save()
    {
        if (!Directory.Exists(AppDirectory))
            Directory.CreateDirectory(AppDirectory);
        SettingsModel.Port = Port;
        SettingsModel.IpAddress = IpAddress;
        SettingsModel.ApiKey = ApiKey;
        SettingsModel.ApiUrl = ApiUrl;
        SettingsModel.Model = Model;
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(SettingsModel, options);
        File.WriteAllText(FilePath, json);
    }
}