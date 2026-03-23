using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SkiaSharp;
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

    [ObservableProperty] private bool _isLoading;

    public async Task Load()
    {
        if (!File.Exists(FilePath))
        {
            SettingsModel = new SettingsModel();
            return;
        }
        IsLoading = true;
        var json = await File.ReadAllTextAsync(FilePath);
        SettingsModel = JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();

        // AI Settings
        Port = SettingsModel.Port;
        IpAddress = SettingsModel.IpAddress;
        ApiKey = SettingsModel.ApiKey;
        ApiUrl = SettingsModel.ApiUrl;
        AiModel = SettingsModel.AIModel;
        IsServerQuery = SettingsModel.IsServerQuery;
        
        // General Settings
        SelectedTheme = SettingsModel.Theme;
        SelectedColor = SettingsModel.AccentColor;
        RefreshFonts();
        RefreshMonoFonts();
        SelectedAppLanguage = SettingsModel.AppLanguage;
        SelectedLanguage = SettingsModel.Language;

        // Voice Settings
        SelectedSource = SettingsModel.Source;
        SelectedVoiceModel = VoiceService.LanguageModels[SelectedLanguage].Contains(SettingsModel.VoiceModel) ?
            SettingsModel.VoiceModel : VoiceService.LanguageModels[SelectedLanguage][0];
        SelectedSpeaker = SettingsModel.Speaker;
        Volume = SettingsModel.Volume;
        Bass = SettingsModel.Bass;
        Treble = SettingsModel.Treble;
        Pitch = SettingsModel.Pitch;

        // 3D Model Settings
        if (Directory.Exists(SettingsModel.Model3DFolder))
            Model3DFolder = SettingsModel.Model3DFolder;
        else
            Directory.CreateDirectory(Model3DFolder);
        RefreshModels3D();

        // Servers
        

        IsLoading = false;
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
        SettingsModel.Theme = SelectedTheme;
        SettingsModel.Font = SelectedFont;
        SettingsModel.MonospaceFont = SelectedMonoFont;
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
        SettingsModel.Model3DFolder = Model3DFolder;

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(SettingsModel, options);
        File.WriteAllText(FilePath, json);
    }

    public async Task InitializeSpeakers() => 
        await RefreshSpeakersAsync(SelectedVoiceModel, SettingsModel.Speaker);

    public async Task InitializeModel3D()
    {
        ModelService.SetBackground();
        await ChangeModel3D(SelectedModel3D);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (IsLoading)
            return;
        Save();
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        Models.Clear();
        var models = VoiceService.LanguageModels[SelectedLanguage];
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
    
    [ObservableProperty] private int _selectedTheme;
    [ObservableProperty] private string _selectedColor;

    public ObservableCollection<string> Fonts { get; } = [];

    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(SelectedFontFamily))]
    private string _selectedFont;

    public FontFamily SelectedFontFamily => new FontFamily(SelectedFont);

    public ObservableCollection<string> MonospaceFonts { get; } = [];
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(SelectedMonoFontFamily))]
    private string _selectedMonoFont;

    public FontFamily SelectedMonoFontFamily => new FontFamily(SelectedMonoFont);

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

    partial void OnSelectedThemeChanged(int value)
    {
        Application.Current!.RequestedThemeVariant = value switch
        {
            0 => ThemeVariant.Light,
            1 => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
        if (IsLoading)
            return;
        ModelService.SetBackground();
    }

    [RelayCommand]
    private void RefreshFonts()
    {
        var allFonts = FontManager.Current.SystemFonts
            .Select(f => f.Name)
            .OrderBy(name => name)
            .ToList();
        var currentFont = SettingsModel.Font;
        Fonts.Clear();
        foreach (var font in allFonts)
            Fonts.Add(font);
        var defaultFontName = FontManager.Current.DefaultFontFamily.Name;
        SelectedFont = allFonts.Contains(currentFont) ? currentFont : defaultFontName;
    }

    [RelayCommand]
    private void RefreshMonoFonts()
    {
        var stopwatch = Stopwatch.StartNew(); 
        var allFonts = FontManager.Current.SystemFonts
            .Select(f => f.Name)
            .Where(name => name.Contains("Mono", StringComparison.OrdinalIgnoreCase) || 
                name.Contains("Code", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Console", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name)
            .ToList();
        if (allFonts.Count <= 0)
            return;
        var currentFont = SettingsModel.MonospaceFont;
        MonospaceFonts.Clear();
        foreach (var font in allFonts)
            MonospaceFonts.Add(font);
        SelectedMonoFont = allFonts.Contains(currentFont) ? currentFont : allFonts[0];
        stopwatch.Stop();
        Console.WriteLine(stopwatch.ElapsedMilliseconds);
    }

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
        var models = VoiceService.LanguageModels[SelectedLanguage];
        foreach (var model in models)
            Models.Add(model);
        if (Models.Count > 0)
            SelectedVoiceModel = Models[0];
    }

    partial void OnSelectedVoiceModelChanged(string value)
    {
        if (IsLoading || string.IsNullOrEmpty(value)) 
            return;
        _ = RefreshSpeakersAsync(value, SelectedSpeaker);
    }

    private async Task RefreshSpeakersAsync(string modelName, string restoreSpeaker)
    {
        IsSpeakersLoading = true;
        var list = await VoiceService.GetSpeakers(modelName);
        IsSpeakersLoading = false;
        Speakers.Clear();
        foreach (var speaker in list)
            Speakers.Add(speaker);
        if (Speakers.Count > 0 && !Speakers.Contains(restoreSpeaker))
            SelectedSpeaker = Speakers[0];
        else
            SelectedSpeaker = restoreSpeaker;
    }



    partial void OnVolumeChanged(double value)
    {
        ValidateProperty(value, nameof(Volume));
    }

    #endregion

    #region Model3D

    private static readonly string WebAssets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets");

    private static readonly string BaseModel3DFolder = 
        Path.Combine(WebAssets, "models");

    [ObservableProperty] private string _model3DFolder = BaseModel3DFolder;

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
        if (!File.Exists(targetPath))
            File.Copy(fullPath, targetPath);
        Models3D.Add(fileName);
    }

    [RelayCommand]
    private async Task ChangeModel3DFolder()
    {
        var topLevel = TopLevel
            .GetTopLevel((Application.Current?.ApplicationLifetime as 
                IClassicDesktopStyleApplicationLifetime)?.MainWindow);
        if (topLevel is null) 
            return;
        var directories = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });
        if (directories.Count <= 0)
            return;
        Model3DFolder = directories[0].Path.LocalPath;
        RefreshModels3D();
    }

    [ObservableProperty] private bool _isModel3DLoading;

    partial void OnSelectedModel3DChanged(string value)
    {
        if (IsLoading)
            return;
        _ = ChangeModel3D(value);
    }

    private async Task ChangeModel3D(string modelFileName)
    {
        IsModel3DLoading = true;
        Directory.CreateDirectory(BaseModel3DFolder);
        string url;
        string newFileName = string.Empty;
        if (Model3DFolder != BaseModel3DFolder)
        {
            // set new temp
            var time = DateTime.Now.ToString("dd_MM_yyyy_HH_mm_ss");
            newFileName = $"temp_{time}.vrm";
            var source = Path.Combine(Model3DFolder, modelFileName);
            var target = Path.Combine(BaseModel3DFolder, newFileName);
            File.Copy(source, target, true);
            url = $"./models/{newFileName}";
        }
        else
            url = $"./models/{modelFileName}";
        string script = $"window.vrmApp.changeModel('{url}')";
        WeakReferenceMessenger.Default.Send(new ExecuteScriptMessage(script));
        while (IsModel3DLoading)
        {
            await Task.Delay(2000);
            try
            {
                var message = new EvaluateScriptMessage("return window.vrmApp.isModelLoaded");
                await WeakReferenceMessenger.Default.Send(message);
                var responce = await message.Response;
                if (responce is Task<int> internalTask)
                {
                    int status = await internalTask;
                    if (status == 0)
                        continue;
                    IsModel3DLoading = false;
                    if (Model3DFolder == BaseModel3DFolder)
                        continue;
                    // remove old or new temp
                    var files = Directory.GetFiles(BaseModel3DFolder, "temp_*.vrm");
                    if (files.Length <= 0)
                        continue;
                    if (status == 1)
                    {
                        foreach (var file in files)
                            if (Path.GetFileName(file) != newFileName)
                                File.Delete(file); 
                    }  
                    else if (status == -1)
                        foreach (var file in files)
                            File.Delete(file); 
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в опросе: {ex.Message}");
            }
        }
    }
    
    private void RefreshModels3D()
    {
        Models3D.Clear();
        var files = Directory.GetFiles(Model3DFolder, "*.vrm");
        foreach (var file in files)
            Models3D.Add(Path.GetFileName(file));
        if (Models3D.Contains(SettingsModel.SelectedModel3D))
            SelectedModel3D = SettingsModel.SelectedModel3D;
        else if (Models3D.Count > 0)
            SelectedModel3D = Models3D[0];
    }

    #endregion

    
}