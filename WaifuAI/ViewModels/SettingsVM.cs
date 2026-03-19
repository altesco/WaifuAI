using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WaifuAI.Models;
using WaifuAI.Services;
using WebViewControl;

namespace WaifuAI.ViewModels;

public partial class SettingsVM : ObservableValidator
{
    private SettingsVM() { }
    
    private static SettingsVM? _instance;
    public static SettingsVM Instance => _instance ??= new SettingsVM();
    
    public static readonly string AppDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "WaifuAI"
    );

    private static readonly string FilePath = Path.Combine(AppDirectory, "settings.json");

    private SettingsModel SettingsModel { get; set; }

    private bool _isLoading;

    public async Task Load()
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
        
        // General Settings
        SelectedAppLanguage = SettingsModel.AppLanguage;
        SelectedLanguage = SettingsModel.Language;

        // Voice Settings
        SelectedSource = SettingsModel.Source;
        SelectedVoiceModel = SettingsModel.VoiceModel;
        await RefreshSpeakersAsync(SelectedVoiceModel, SettingsModel.Speaker);
        SelectedSpeaker = SettingsModel.Speaker;
        Volume = SettingsModel.Volume;
        Bass = SettingsModel.Bass;
        Treble = SettingsModel.Treble;
        Pitch = SettingsModel.Pitch;

        // 3D Model Settings
        if (Directory.Exists(Model3DFolder))
        {
            var files = Directory.GetFiles(Model3DFolder);
            foreach (var file in files)
                Models3D.Add(Path.GetFileName(file));
            if (Models3D.Contains(SettingsModel.SelectedModel3D))
                SelectedModel3D = SettingsModel.SelectedModel3D;
            else if (Models3D.Count > 0)
                SelectedModel3D = Models3D[0];
        }

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

        // General Settings
        SettingsModel.AppLanguage = SelectedAppLanguage;
        SettingsModel.Language = SelectedLanguage;

        // Voice Settings
        SettingsModel.Source = SelectedSource;
        SettingsModel.VoiceModel = SelectedVoiceModel;
        SettingsModel.Speaker = SelectedSpeaker;
        SettingsModel.Volume = Volume;
        SettingsModel.Bass = Bass;
        SettingsModel.Treble = Treble;
        SettingsModel.Pitch = Pitch;

        // 3D Model Settings
        SettingsModel.SelectedModel3D = SelectedModel3D;

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

    partial void OnSelectedLanguageChanged(string value)
    {
        Models.Clear();
        var models = QueryService.GetModels(SelectedLanguage);
        foreach (var model in models)
            Models.Add(model);
        if (Models.Count > 0)
            SelectedVoiceModel = Models[0];
    }

    #region AISettings

    [ObservableProperty] private bool _isServerQuery;
    [ObservableProperty] private int _port;
    [ObservableProperty] private string _ipAddress;
    [ObservableProperty] private string _apiKey;
    [ObservableProperty] private string _apiUrl;
    [ObservableProperty] private string _aiModel;

    #endregion

    #region GeneralSettings

    public ObservableCollection<string> AppLanguages { get; } =
    [
        "ru", "en"
    ];
    [ObservableProperty] private string _selectedAppLanguage = "ru";

    public ObservableCollection<string> Languages { get; } =
    [
        "ru", "en", "de", "es", "fr"
    ];
    [ObservableProperty] private string _selectedLanguage = "ru";

    #endregion

    #region VoiceSettings

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

    [ObservableProperty] [Range(-10, 10)] private double _treble;
    [ObservableProperty] [Range(-10, 10)] private double _bass;
    [ObservableProperty] [Range(0, 2)] private double _pitch;
    [ObservableProperty] private bool _isSpeakersLoading;

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
        if (_isLoading || string.IsNullOrEmpty(value)) 
            return;
        _ = RefreshSpeakersAsync(value);
    }

    private async Task RefreshSpeakersAsync(string modelName, string? restoreSpeaker = null)
    {
        IsSpeakersLoading = true;
        var list = await QueryService.GetSpeakers(modelName);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Speakers.Clear();
            foreach (var speaker in list)
                Speakers.Add(speaker);
            if (!string.IsNullOrEmpty(restoreSpeaker) && Speakers.Contains(restoreSpeaker))
            {
                SelectedSpeaker = restoreSpeaker;
            }
            else if (Speakers.Count > 0)
            {
                SelectedSpeaker = Speakers[0];
            }
        });
        IsSpeakersLoading = false;
    }

    partial void OnVolumeChanged(double value)
    {
        ValidateProperty(value, nameof(Volume));
    }

    #endregion

    #region Model3D

    [ObservableProperty] private string _model3DFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets", "models");

    public ObservableCollection<string> Models3D { get; } = [];
    [ObservableProperty] private string _selectedModel3D;

    [RelayCommand]
    private async Task OpenModel3DFile()
    {
        var topLevel = TopLevel
            .GetTopLevel((Application.Current?.ApplicationLifetime as 
                IClassicDesktopStyleApplicationLifetime)?.MainWindow);
        if (topLevel is null) 
            return;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите VRM модель",
            AllowMultiple = false,
            FileTypeFilter = 
            [ 
                new FilePickerFileType("VRM Model")
                {
                    Patterns = [ "*.vrm" ]
                } 
            ]
        });
        if (files.Count <= 0)
            return;
        var selectedFile = files[0];
        string fullPath = selectedFile.Path.LocalPath;
        string fileName = selectedFile.Name;
        Directory.CreateDirectory(Model3DFolder);
        string targetPath = Path.Combine(Model3DFolder, fileName);
        File.Copy(fullPath, targetPath, true);
        Models3D.Add(fileName);
    }

    partial void OnSelectedModel3DChanged(string value)
    {
        string urlForJs = $"./models/{value}";
        WeakReferenceMessenger.Default.Send(
            new ExecuteScriptMessage(
                $"window.vrmApp.changeModel('{urlForJs}')"
                ));
    }

    #endregion

}