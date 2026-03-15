using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaifuAI.Models;
using WaifuAI.Services;

namespace WaifuAI.ViewModels;

public partial class SettingsVM : ObservableValidator
{
    private SettingsVM() { }
    
    private static SettingsVM? _instance;
    public static SettingsVM Instance => _instance ??= new SettingsVM();
    
    private static readonly string AppDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "WaifuAI"
    );

    private static readonly string FilePath = Path.Combine(AppDirectory, "settings.json");

    private SettingsModel SettingsModel { get; set; }

    private bool _isLoading;

    public void Load()
    {
        if (!File.Exists(FilePath))
        {
            SettingsModel = new SettingsModel();
            return;
        }
        _isLoading = true;
        var json = File.ReadAllText(FilePath);
        SettingsModel = JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();

        // AI Settings
        Port = SettingsModel.Port;
        IpAddress = SettingsModel.IpAddress;
        ApiKey = SettingsModel.ApiKey;
        ApiUrl = SettingsModel.ApiUrl;
        AiModel = SettingsModel.AIModel;
        IsServerQuery = SettingsModel.IsServerQuery;
        
        // Voice Settings
        SelectedSource = SettingsModel.Source;
        SelectedVoiceModel = SettingsModel.VoiceModel;
        SelectedSpeaker = SettingsModel.Speaker;
        Volume = SettingsModel.Volume;
        Bass = SettingsModel.Bass;
        Treble = SettingsModel.Treble;

        _isLoading = false;
    }

    private void Save()
    {
        if (!Directory.Exists(AppDirectory))
            Directory.CreateDirectory(AppDirectory);
        
        // AI Settings
        SettingsModel.Port = Port;
        SettingsModel.IpAddress = IpAddress;
        SettingsModel.ApiKey = ApiKey;
        SettingsModel.ApiUrl = ApiUrl;
        SettingsModel.AIModel = AiModel;
        SettingsModel.IsServerQuery = IsServerQuery;

        // Voice Settings
        SettingsModel.Source = SelectedSource;
        SettingsModel.VoiceModel = SelectedVoiceModel;
        SettingsModel.Speaker = SelectedSpeaker;
        SettingsModel.Volume = Volume;
        SettingsModel.Bass = Bass;
        SettingsModel.Treble = Treble;

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(SettingsModel, options);
        File.WriteAllText(FilePath, json);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_isLoading)
            return;
        Save();
    }

    #region AISettings

    [ObservableProperty] private bool _isServerQuery;
    [ObservableProperty] private int _port;
    [ObservableProperty] private string _ipAddress;
    [ObservableProperty] private string _apiKey;
    [ObservableProperty] private string _apiUrl;
    [ObservableProperty] private string _aiModel;

    #endregion

    #region VoiceSettings

    public ObservableCollection<string> Languages { get; } =
    [
        "ru", "en", "de", "es", "fr"
    ];
    [ObservableProperty] private string _selectedLanguage = "ru";

    public ObservableCollection<string> Sources { get; } =
    [
        "silero_tts"
    ];
    [ObservableProperty] private string _selectedSource;

    public ObservableCollection<string> Models { get; set; } = [];
    [ObservableProperty] private string _selectedVoiceModel;

    public ObservableCollection<string> Speakers { get; set; } = [];
    [ObservableProperty] private string _selectedSpeaker;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumeLevel))]
    [Range(0, 2)]
    private double _volume;
    
    public int VolumeLevel
    {
        get
        {
            if (Volume > 1.4)
                return 3;
            if (Volume > 0.7)
                return 2;
            if (Volume > 0)
                return 1;
            return 0;
        }
    }

    [ObservableProperty] 
    [Range(-10, 10)]
    private double _treble;

    [ObservableProperty] 
    [Range(-10, 10)]
    private double _bass;

    partial void OnSelectedSourceChanged(string value)
    {
        Models.Clear();
        var models = QueryService.GetModels(SelectedLanguage);
        foreach (var model in models)
            Models.Add(model);
        if (Models.Count > 0)
            SelectedVoiceModel = Models[0];
    }

    partial void OnSelectedVoiceModelChanged(string value)
    {
        Console.WriteLine(value);
        _ = Task.Run(async () =>
        {
            var list = await QueryService.GetSpeakers(SelectedVoiceModel);
            Speakers.Clear();
            foreach (var speaker in list)
                Speakers.Add(speaker);
        });
    }
    partial void OnVolumeChanged(double value)
    {
        ValidateProperty(value, nameof(Volume));
    }

    #endregion
}