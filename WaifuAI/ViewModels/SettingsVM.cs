using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WaifuAI.Models;
using WaifuAI.Services;

namespace WaifuAI.ViewModels;

public partial class SettingsVM : ObservableValidator
{
    private static SettingsVM? _instance;
    public static SettingsVM Instance => _instance ??= new SettingsVM();
    
    public static readonly string AppDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "WaifuAI"
    );

    private static readonly string FilePath = Path.Combine(AppDirectory, "settings.json");

    public static readonly string PromptsPath = Path.Combine(AppDirectory, "Prompts");

    public static readonly string KnowledgeBasePath = Path.Combine(AppDirectory, "knowledge_base.db");

    public static readonly string HistoryPath = Path.Combine(AppDirectory, "history.db");

    private SettingsModel SettingsModel { get; set; }

    [ObservableProperty] private bool _isSettingsLoading;
    [ObservableProperty] private bool _isAppInitializing = true;

    public async Task Load()
    {
        IsSettingsLoading = true;

        if (File.Exists(FilePath))
        {
            var json = await File.ReadAllTextAsync(FilePath);
            SettingsModel = JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();
        }
        else
            SettingsModel = new SettingsModel();

        // AI Parameters
        Temperature = SettingsModel.Temperature;
        ResponseLength = SettingsModel.ResponseLength;
        MaxTokens = SettingsModel.MaxTokens;
        ContextLength = SettingsModel.ContextLength;

        // Server Settings
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
        SelectedVoiceModel = VoiceService.LanguageModels[SelectedLanguage].Contains(SettingsModel.VoiceModel) 
            ? SettingsModel.VoiceModel 
            : VoiceService.LanguageModels[SelectedLanguage][0];
        SelectedSpeaker = SettingsModel.Speaker; 
        Volume = SettingsModel.Volume; 
        Bass = SettingsModel.Bass; 
        Treble = SettingsModel.Treble; 
        Pitch = SettingsModel.Pitch; 
        IsStream = SettingsModel.IsStream;

        // 3D Model Settings
        if (Directory.Exists(SettingsModel.Model3DFolder))
            Model3DFolder = SettingsModel.Model3DFolder;
        else
            Directory.CreateDirectory(Model3DFolder);
        RefreshModels3D();
        Camera = SettingsModel.Camera;

        // Personality
        WaifuName = SettingsModel.WaifuName;
        Birthday = SettingsModel.Birthday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        SelectedArchetype = 
            Archetypes.Find(x => x.Name == SettingsModel.SelectedArchetype) ?? 
            Archetypes[0];
        
        // Emotional State System
        Affection = SettingsModel.Affection;
        Engagement = SettingsModel.Engagement;
        Mood = SettingsModel.Mood;
        Energy = SettingsModel.Energy;

        // Prompts
        Directory.CreateDirectory(PromptsPath);
        foreach (var archetype in Archetypes)
        {
            var promptPath = Path.Combine(PromptsPath, $"{archetype.Name}.txt");
            if (!File.Exists(promptPath))
            {
                var prompt = PromptService.GetArchetypePrompt(archetype);
                await File.WriteAllTextAsync(promptPath, prompt);
                archetype.Prompt = prompt;
            }
            else
                archetype.Prompt = await File.ReadAllTextAsync(promptPath);
        }

        // Status System
        UserName = SettingsModel.UserName;
        IsDating = SettingsModel.IsDating;

        // Drop Times
        LastEngagementDrop = SettingsModel.LastEngagementDrop;
        LastEnergyDrop = SettingsModel.LastEnergyDrop;

        IsSettingsLoading = false;
    }

    private void Save()
    {
        if (!Directory.Exists(AppDirectory))
            Directory.CreateDirectory(AppDirectory);
        
        // AI Parameters
        SettingsModel.Temperature = Temperature;
        SettingsModel.ResponseLength = ResponseLength;
        SettingsModel.MaxTokens = MaxTokens;
        SettingsModel.ContextLength = ContextLength;

        // Server Settings
        SettingsModel.Port = Port; 
        SettingsModel.IpAddress = IpAddress;       
        SettingsModel.ApiKey = ApiKey;       
        SettingsModel.ApiUrl = ApiUrl;       
        SettingsModel.AIModel = AiModel;      
        SettingsModel.IsServerQuery = IsServerQuery;       

        // General Settings
        SettingsModel.Theme = SelectedTheme;       
        SettingsModel.AccentColor = SelectedColor;       
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
        SettingsModel.IsStream = IsStream;

        // 3D Model Settings
        SettingsModel.SelectedModel3D = SelectedModel3D;       
        SettingsModel.Model3DFolder = Model3DFolder;
        SettingsModel.Camera = Camera;

        // Personality Settings
        SettingsModel.WaifuName = WaifuName;
        SettingsModel.Birthday = DateOnly.ParseExact(Birthday, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        SettingsModel.SelectedArchetype = SelectedArchetype.Name;

        // Emotional State System
        SettingsModel.Affection = Affection;
        SettingsModel.Engagement = Engagement;
        SettingsModel.Mood = Mood;
        SettingsModel.Energy = Energy;

        // Status System
        SettingsModel.UserName = UserName;
        SettingsModel.IsDating = IsDating;

        // Drop Times
        SettingsModel.LastEngagementDrop = LastEngagementDrop;
        SettingsModel.LastEnergyDrop = LastEnergyDrop;

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(SettingsModel, options);
        File.WriteAllText(FilePath, json);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (IsSettingsLoading)
            return;
        Save();
    }

    #region AIParameters
    
    [ObservableProperty] 
    [NotifyDataErrorInfo]
    [Range(0.0, 1.0)]
    private double _temperature;

    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(ResponseLengthValue))]
    private ResponseLength _responseLength;

    public double ResponseLengthValue
    {
        get => (double)ResponseLength;
        set
        {
            var intValue = (int)Math.Round(value);

            if (!Enum.IsDefined(typeof(ResponseLength), intValue)) 
                return;
            
            var newLength = (ResponseLength)intValue;
            if (ResponseLength != newLength)
                ResponseLength = newLength;
        }
    }

    [ObservableProperty]
    [NotifyDataErrorInfo]    
    [Range(0, int.MaxValue)] 
    private int _maxTokens;
    
    [ObservableProperty] 
    [NotifyDataErrorInfo]
    [Range(0, int.MaxValue)] 
    private int _contextLength;

    #endregion

    #region ServerSettings

    [ObservableProperty] private bool _isServerQuery;
    [ObservableProperty] private int _port;
    [ObservableProperty] private string _ipAddress;
    [ObservableProperty] private string _apiKey;
    [ObservableProperty] private string _apiUrl;
    [ObservableProperty] private string _aiModel;

    #endregion

    #region GeneralSettings
    
    [ObservableProperty] private int _selectedTheme = -1;

    [ObservableProperty] 
    [NotifyDataErrorInfo]
    [RegularExpression("^#([A-Fa-f0-9]{3}|[A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$")]
    private string _selectedColor;

    partial void OnSelectedColorChanged(string value)
    {
        var app = Application.Current;
        var theme = app?.Styles.OfType<FluentTheme>().FirstOrDefault();
        if (HasErrors || app is null || theme is null || !Color.TryParse(value, out var color))
            return;
        if (theme.Palettes.TryGetValue(ThemeVariant.Light, out var lightPalette) &&
            lightPalette is { } light)
            light.Accent = color;
        if (theme.Palettes.TryGetValue(ThemeVariant.Dark, out var darkPalette) &&
            darkPalette is { } dark)
            dark.Accent = color;
        app.Resources["SystemAccentColorDark1"] = CreateLighterColor(color, -0.1);
        app.Resources["SystemAccentColorDark2"] = CreateLighterColor(color, -0.2);
        app.Resources["SystemAccentColorDark3"] = CreateLighterColor(color, -0.3);
        app.Resources["SystemAccentColorLight1"] = CreateLighterColor(color, 0.1);
        app.Resources["SystemAccentColorLight2"] = CreateLighterColor(color, 0.2);
        app.Resources["SystemAccentColorLight3"] = CreateLighterColor(color, 0.3);
    }

    private Color CreateLighterColor(Color baseColor, double factor)
    {
        return Color.FromArgb(baseColor.A,
            (byte)Math.Clamp(baseColor.R + (255 * factor), 0, 255),
            (byte)Math.Clamp(baseColor.G + (255 * factor), 0, 255),
            (byte)Math.Clamp(baseColor.B + (255 * factor), 0, 255));
    }

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

    partial void OnSelectedLanguageChanged(string value)
    {
        if (!VoiceService.LanguageModels.ContainsKey(value))
        {
            var systemLanguage = CultureInfo.CurrentCulture.TwoLetterISOLanguageName.ToLower();
            SelectedLanguage = systemLanguage is "ru" or "en" or "de" or "es" or "fr"
                    ? systemLanguage
                    : "en";
            return;
        }
        Models.Clear();
        var models = VoiceService.LanguageModels[value];
        foreach (var model in models)
            Models.Add(model);
        if (Models.Count > 0)
            SelectedVoiceModel = Models[0];
    }

    partial void OnSelectedThemeChanged(int value)
    {
        Application.Current!.RequestedThemeVariant = value switch
        {
            0 => ThemeVariant.Light,
            1 => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
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

    public async Task InitializeSpeakers() => 
        await RefreshSpeakersAsync(SelectedVoiceModel, SettingsModel.Speaker);

    public static readonly string HomePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static readonly string VoiceModelFolder = Path.Combine(
        HomePath, 
        ".cache", 
        "torch", 
        "hub", 
        "snakers4_silero-models_master", 
        "src", 
        "silero", 
        "model"
    );

    public ObservableCollection<string> Sources { get; } =
    [
        "silero_tts"
    ];
    [ObservableProperty] private string _selectedSource;

    public ObservableCollection<string> Models { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectedVoiceModelLoaded))]
    private string _selectedVoiceModel;

    public bool IsSelectedVoiceModelLoaded => File.Exists(Path.Combine(VoiceModelFolder, $"{SelectedVoiceModel}.pt"));

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

    [ObservableProperty] private bool _isStream;

    [ObservableProperty] private bool _isSpeakersLoading;
    [ObservableProperty] private long _voiceModelSize;
    [ObservableProperty] private bool _isDownloading;
    private CancellationTokenSource? _downloadCts;
    [ObservableProperty] private double _downloadProgress;
    [ObservableProperty] private double _downloadSpeed;

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
        if (IsSettingsLoading || string.IsNullOrEmpty(value))
            return;
        _ = RefreshVoiceModelInfo();
        _ = RefreshSpeakersAsync(value, SelectedSpeaker);
    }

    private async Task RefreshSpeakersAsync(string modelName, string restoreSpeaker)
    {
        if (!IsSelectedVoiceModelLoaded)
            return;
        IsSpeakersLoading = true;
        var modelPath = Path.Combine(VoiceModelFolder, $"{modelName}.pt");
        var list = await VoiceService.GetSpeakers(modelPath, SelectedLanguage);
        IsSpeakersLoading = false;
        Speakers.Clear();
        foreach (var speaker in list)
            Speakers.Add(speaker);
        if (list.Count <= 0)
            return;
        SelectedSpeaker = Speakers.Contains(restoreSpeaker)
            ? Speakers[Speakers.IndexOf(restoreSpeaker)]
            : Speakers[0];
    }

    partial void OnVolumeChanged(double value)
    {
        ValidateProperty(value, nameof(Volume));
    }

    private async Task RefreshVoiceModelInfo()
    {
        var url = VoiceService.ModelsUrls[SelectedVoiceModel];
        var request = new HttpRequestMessage(HttpMethod.Head, url);
        var responce = await ApiService.HttpClient.SendAsync(request);
        responce.EnsureSuccessStatusCode();
        var size = responce.Content.Headers.ContentLength;
        if (size != null)
            VoiceModelSize = (long)size;
    }
    
    [RelayCommand]
    private async Task DownloadVoiceModel()
    {
        _downloadCts = new CancellationTokenSource();
        var token = _downloadCts.Token;
        var url = VoiceService.ModelsUrls[SelectedVoiceModel];
        var fullPath = Path.Combine(VoiceModelFolder, $"{SelectedVoiceModel}.pt");
        try
        {
            IsDownloading = true;
            Directory.CreateDirectory(VoiceModelFolder);
            using var response =
                await ApiService.HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            await using var contentStream = await response.Content.ReadAsStreamAsync(token);
            await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            var buffer = new byte[1024 * 1024];
            long totalRead = 0;
            long lastRead = 0;
            var sw = Stopwatch.StartNew();
            int read;
            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read, token);
                totalRead += read;
                DownloadProgress = Math.Round((double)totalRead / VoiceModelSize * 100);
                if (sw.ElapsedMilliseconds < 1000)
                    continue;
                DownloadSpeed = Math.Round((double)(totalRead - lastRead) / 1024 / 1024, 2);
                lastRead = totalRead;
                sw.Restart();
            }
            sw.Stop();
            // for speakers refresh and notify about download status
            OnPropertyChanged(nameof(IsSelectedVoiceModelLoaded));
            await RefreshSpeakersAsync(SelectedVoiceModel, SelectedSpeaker);
            SelectedSpeaker = Speakers[0]; // because idk why but it is not refresh in the method above
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
        finally
        {
            IsDownloading = false;
            _downloadCts.Dispose();
            _downloadCts = null;
            DownloadProgress = 0;
            DownloadSpeed = 0;
        }
    }

    [RelayCommand]
    private void CancelDownload() => _downloadCts?.Cancel();

    #endregion

    #region Model3D

    public async Task InitializeModel3D()
    {
        ModelService.SetBackground();
        await ChangeModel3D(SelectedModel3D);
        ModelService.SetCamera(Camera);
        _ = RefreshVoiceModelInfo();
    }

    private static readonly string WebAssets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets");

    private static readonly string BaseModel3DFolder = 
        Path.Combine(WebAssets, "models");

    [ObservableProperty] private string _model3DFolder = BaseModel3DFolder;

    public ObservableCollection<string> Models3D { get; } = [];
    [ObservableProperty] private string _selectedModel3D;

    [ObservableProperty] private CameraVariant _camera;

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
        if (IsSettingsLoading)
            return;
        _ = ChangeModel3D(value);
    }

    partial void OnCameraChanged(CameraVariant value)
    {
        if (IsSettingsLoading)
            return;
        
        _ = Task.Run(async () =>
        {
            ModelService.SetCamera(value);

            await Task.Delay(30);

            WeakReferenceMessenger.Default.Send(new SnapshotMessage(true));
        });
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
                var message = new EvaluateScriptMessage<int>("return window.vrmApp.isModelLoaded");
                int status = await WeakReferenceMessenger.Default.Send(message);

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

    #region PersonalitySettings

    [ObservableProperty] private string _waifuName;

    [ObservableProperty] 
    [RegularExpression(@"^\d{2}\-\d{2}\-\d{4}$", ErrorMessage = "Формат должен быть ГГГГ-ММ-ДД")]
    [CustomValidation(typeof(SettingsVM), nameof(ValidateRealDate))]
    private string _birthday;

    public static ValidationResult? ValidateRealDate(string? dateStr, ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(dateStr) || dateStr.Length != 10)
            return ValidationResult.Success;

        bool isRealDate = DateTime.TryParseExact(
            dateStr,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedDate
        );

        if (!isRealDate)
            return new ValidationResult("Такой даты не существует (например, некорректный день или месяц)");

        if (Helper.GetAge(DateOnly.FromDateTime(parsedDate)) < 18)
            return new ValidationResult("Недопустимый возраст");

        return ValidationResult.Success;
    }

    public List<ArchetypeVM> Archetypes { get; } =
    [
        new()
        {
            Name = "tsundere",
            Description =
                "Колючая снаружи, но мягкая и заботливая внутри. Проявляет симпатию через напускную грубость.",
            Emoji = "🔥",
            Color = Color.Parse("#ff5c5c"),
            BaseMoodVector = new MoodVector
            {
                Affection = (-3, 2),
                Engagement = (-5, 10),
                Mood = (-8, 8),
                Energy = (-2, 1)
            },
            Sensitivity = new ArchetypeSensitivity
            {
                AbsenceAffectionImpact = -2.0f, DaysAffectionBonus = 4.0f,
                AbsenceEngagementImpact = -5.0f, DaysEngagementBonus = 2.0f,
                AbsenceMoodImpact = -8.0f, DaysMoodBonus = 1.5f,
                AbsenceEnergyImpact = -1.0f, DaysEnergyBonus = 0.0f,
                DaysSaturation = 35,
                MessageSaturation = 900,
                AbsenceTauHours = 24f,
                ResponseQuestionChance = 0.35f,
                EngagementDropChance = 0.35f,
                EngagementFloor = 25,
                EngagementDropRange = (5, 15)
            }
        },
        new()
        {
            Name = "kuudere",
            Description =
                "Хладнокровная, молчаливая и внешне безэмоциональная. Скрывает глубокие чувства за маской апатии.",
            Emoji = "🧊",
            Color = Color.Parse("#0008ff"),
            BaseMoodVector = new MoodVector
            {
                Affection = (-1, 2),
                Engagement = (-2, 5),
                Mood = (-2, 3),
                Energy = (-1, 1)
            },
            Sensitivity = new ArchetypeSensitivity
            {
                AbsenceAffectionImpact = -0.5f, DaysAffectionBonus = 1.5f,
                AbsenceEngagementImpact = -1.0f, DaysEngagementBonus = 1.0f,
                AbsenceMoodImpact = -0.5f, DaysMoodBonus = 0.5f,
                AbsenceEnergyImpact = -0.5f, DaysEnergyBonus = 0.0f,
                DaysSaturation = 50,
                MessageSaturation = 500,
                AbsenceTauHours = 48f,
                ResponseQuestionChance = 0.2f,
                EngagementDropChance = 0.15f,
                EngagementFloor = 30,
                EngagementDropRange = (3, 10)
            }
        },
        new()
        {
            Name = "dandere",
            Description =
                "Крайне стеснительная и молчаливая личность. Раскрывается только в узком кругу тех, кому доверяет.",
            Emoji = "😳",
            Color = Color.Parse("#544dc2"),
            BaseMoodVector = new MoodVector
            {
                Affection = (-2, 3),
                Engagement = (-3, 8),
                Mood = (-4, 5),
                Energy = (-3, 1)
            },
            Sensitivity = new ArchetypeSensitivity
            {
                AbsenceAffectionImpact = -1.0f, DaysAffectionBonus = 5.0f,
                AbsenceEngagementImpact = -3.0f, DaysEngagementBonus = 3.0f,
                AbsenceMoodImpact = -2.0f, DaysMoodBonus = 2.0f,
                AbsenceEnergyImpact = -1.5f, DaysEnergyBonus = 0.5f,
                DaysSaturation = 45,
                MessageSaturation = 600,
                AbsenceTauHours = 36f,
                ResponseQuestionChance = 0.25f,
                EngagementDropChance = 0.3f,
                EngagementFloor = 15,
                EngagementDropRange = (5, 15)
            }
        },
        new()
        {
            Name = "deredere",
            Description =
                "Воплощение чистой любви и оптимизма. Всегда искренняя, теплая и энергично заботится об окружающих.",
            Emoji = "💓",
            Color = Color.Parse("#ff4b8a"),
            BaseMoodVector = new MoodVector
            {
                Affection = (-1, 6),
                Engagement = (-2, 12),
                Mood = (-2, 10),
                Energy = (-1, 2)
            },
            Sensitivity = new ArchetypeSensitivity
            {
                AbsenceAffectionImpact = -1.0f, DaysAffectionBonus = 3.0f,
                AbsenceEngagementImpact = -2.0f, DaysEngagementBonus = 2.5f,
                AbsenceMoodImpact = -3.0f, DaysMoodBonus = 2.0f,
                AbsenceEnergyImpact = -1.0f, DaysEnergyBonus = 0.0f,
                DaysSaturation = 10,
                MessageSaturation = 1500,
                AbsenceTauHours = 16f,
                ResponseQuestionChance = 0.75f,
                EngagementDropChance = 0.05f,
                EngagementFloor = 50,
                EngagementDropRange = (3, 10)
            }
        },
        new()
        {
            Name = "genki",
            Description =
                "Неиссякаемый источник энергии. Жизнерадостная, активная и всегда готова вдохновлять на подвиги.",
            Emoji = "🌞",
            Color = Color.Parse("#dfa017"),
            BaseMoodVector = new MoodVector
            {
                Affection = (-1, 4),
                Engagement = (-2, 15),
                Mood = (-2, 12),
                Energy = (-1, 3)
            },
            Sensitivity = new ArchetypeSensitivity
            {
                AbsenceAffectionImpact = -0.5f, DaysAffectionBonus = 2.0f,
                AbsenceEngagementImpact = -4.0f, DaysEngagementBonus = 3.0f,
                AbsenceMoodImpact = -2.0f, DaysMoodBonus = 1.0f,
                AbsenceEnergyImpact = -0.5f, DaysEnergyBonus = 1.0f,
                DaysSaturation = 7,
                MessageSaturation = 2000,
                AbsenceTauHours = 10f,
                ResponseQuestionChance = 0.85f,
                EngagementDropChance = 0.25f,
                EngagementFloor = 35,
                EngagementDropRange = (5, 20)
            }
        },
        new()
        {
            Name = "yandere",
            Description =
                "Одержимая и пугающе преданная. Готова на любые крайности ради того, чтобы объект любви принадлежал только ей.",
            Emoji = "🔪",
            Color = Color.Parse("#cb0e0e"),
            BaseMoodVector = new MoodVector
            {
                Affection = (-5, 10),
                Engagement = (-10, 15),
                Mood = (-15, 15),
                Energy = (-2, 2)
            },
            Sensitivity = new ArchetypeSensitivity
            {
                AbsenceAffectionImpact = -5.0f, DaysAffectionBonus = 8.0f,
                AbsenceEngagementImpact = -12.0f, DaysEngagementBonus = 4.0f,
                AbsenceMoodImpact = -12.0f, DaysMoodBonus = 3.0f,
                AbsenceEnergyImpact = -2.0f, DaysEnergyBonus = 0.0f,
                DaysSaturation = 5,
                MessageSaturation = 1500,
                AbsenceTauHours = 6f,
                ResponseQuestionChance = 0.65f,
                EngagementDropChance = 0.05f,
                EngagementFloor = 60,
                EngagementDropRange = (3, 10)
            }
        },
        new()
        {
            Name = "teasedere",
            Description =
                "Мастер подколов и легкого кокетства. Обожает смущать собеседника и проявляет чувства через дразнилки.",
            Emoji = "❤️‍🔥",
            Color = Color.Parse("#ff9431"),
            BaseMoodVector = new MoodVector
            {
                Affection = (-2, 4),
                Engagement = (-3, 12),
                Mood = (-3, 8),
                Energy = (-1, 2)
            },
            Sensitivity = new ArchetypeSensitivity
            {
                AbsenceAffectionImpact = -1.0f, DaysAffectionBonus = 3.0f,
                AbsenceEngagementImpact = -5.0f, DaysEngagementBonus = 3.0f,
                AbsenceMoodImpact = -3.0f, DaysMoodBonus = 1.5f,
                AbsenceEnergyImpact = -1.0f, DaysEnergyBonus = 0.0f,
                DaysSaturation = 20,
                MessageSaturation = 1200,
                AbsenceTauHours = 20f,
                ResponseQuestionChance = 0.6f,
                EngagementDropChance = 0.40f,
                EngagementFloor = 20,
                EngagementDropRange = (5, 20)
            }
        },
        new()
        {
            Name = "dorodere",
            Description = "Милая и добрая на первый взгляд, но хранит внутри затаенную обиду или жестокую сторону.",
            Emoji = "⚫",
            Color = Colors.SlateGray,
            BaseMoodVector = new MoodVector
            {
                Affection = (-4, 3),
                Engagement = (-4, 8),
                Mood = (-6, 6),
                Energy = (-2, 1)
            },
            Sensitivity = new ArchetypeSensitivity
            {
                AbsenceAffectionImpact = -3.0f, DaysAffectionBonus = 2.0f,
                AbsenceEngagementImpact = -4.0f, DaysEngagementBonus = 1.5f,
                AbsenceMoodImpact = -7.0f, DaysMoodBonus = 1.0f,
                AbsenceEnergyImpact = -1.5f, DaysEnergyBonus = 0.0f,
                DaysSaturation = 40,
                MessageSaturation = 800,
                AbsenceTauHours = 24f,
                ResponseQuestionChance = 0.4f,
                EngagementDropChance = 0.45f,
                EngagementFloor = 10,
                EngagementDropRange = (8, 25)
            }
        },
        new()
        {
            Name = "utsudere",
            Description =
                "Меланхоличная личность, склонная к грусти и депрессивным настроениям из-за тяжелого прошлого.",
            Emoji = "💧",
            Color = Color.Parse("#0876d6"),
            BaseMoodVector = new MoodVector
            {
                Affection = (-2, 3),
                Engagement = (-5, 5),
                Mood = (-8, 3),
                Energy = (-4, 1)
            },
            Sensitivity = new ArchetypeSensitivity
            {
                AbsenceAffectionImpact = -2.0f, DaysAffectionBonus = 2.5f,
                AbsenceEngagementImpact = -5.0f, DaysEngagementBonus = 1.5f,
                AbsenceMoodImpact = -5.0f, DaysMoodBonus = 1.0f,
                AbsenceEnergyImpact = -3.0f, DaysEnergyBonus = 0.5f,
                DaysSaturation = 30,
                MessageSaturation = 600,
                AbsenceTauHours = 14f,
                ResponseQuestionChance = 0.2f,
                EngagementDropChance = 0.50f,
                EngagementFloor = 10,
                EngagementDropRange = (5, 20)
            }
        },
        new()
        {
            Name = "bakadere",
            Description =
                "Наивная, неуклюжая и очень открытая. Не умеет скрывать чувства и часто попадает в неловкие ситуации.",
            Emoji = "🐔",
            Color = Colors.Brown,
            BaseMoodVector = new MoodVector
            {
                Affection = (-1, 5),
                Engagement = (-2, 10),
                Mood = (-3, 8),
                Energy = (-1, 2)
            },
            Sensitivity = new ArchetypeSensitivity
            {
                AbsenceAffectionImpact = -0.5f, DaysAffectionBonus = 2.0f,
                AbsenceEngagementImpact = -2.0f, DaysEngagementBonus = 1.5f,
                AbsenceMoodImpact = -1.5f, DaysMoodBonus = 1.0f,
                AbsenceEnergyImpact = -1.0f, DaysEnergyBonus = 0.0f,
                DaysSaturation = 7,
                MessageSaturation = 1200,
                AbsenceTauHours = 12f,
                ResponseQuestionChance = 0.5f,
                EngagementDropChance = 0.40f,
                EngagementFloor = 20,
                EngagementDropRange = (5, 15)
            }
        },
        new()
        {
            Name = "darudere",
            Description = "Ленивая и слегка отстраненная. Предпочитает покой и отдых любым активным действиям.",
            Emoji = "💤",
            Color = Color.Parse("#1d6bc5"),
            BaseMoodVector = new MoodVector
            {
                Affection = (-1, 2),
                Engagement = (-5, 4),
                Mood = (-2, 4),
                Energy = (-5, 1)
            },
            Sensitivity = new ArchetypeSensitivity
            {
                AbsenceAffectionImpact = -0.2f, DaysAffectionBonus = 1.0f,
                AbsenceEngagementImpact = -1.0f, DaysEngagementBonus = 0.5f,
                AbsenceMoodImpact = -0.5f, DaysMoodBonus = 0.5f,
                AbsenceEnergyImpact = -2.0f, DaysEnergyBonus = 0.0f,
                DaysSaturation = 30,
                MessageSaturation = 400,
                AbsenceTauHours = 72f,
                ResponseQuestionChance = 0.15f,
                EngagementDropChance = 0.60f,
                EngagementFloor = 10,
                EngagementDropRange = (10, 25)
            }
        },
        new()
        {
            Name = "hinedere",
            Description =
                "Циничная и высокомерная снаружи, но способна измениться, если найдет кого-то достойного доверия.",
            Emoji = "🚬",
            Color = Color.Parse("#318eb0"),
            BaseMoodVector = new MoodVector
            {
                Affection = (-4, 2),
                Engagement = (-4, 6),
                Mood = (-5, 5),
                Energy = (-2, 1)
            },
            Sensitivity = new ArchetypeSensitivity
            {
                AbsenceAffectionImpact = -1.5f, DaysAffectionBonus = 3.5f,
                AbsenceEngagementImpact = -3.0f, DaysEngagementBonus = 2.0f,
                AbsenceMoodImpact = -4.0f, DaysMoodBonus = 1.5f,
                AbsenceEnergyImpact = -1.0f, DaysEnergyBonus = 0.0f,
                DaysSaturation = 60,
                MessageSaturation = 700,
                AbsenceTauHours = 48f,
                ResponseQuestionChance = 0.3f,
                EngagementDropChance = 0.45f,
                EngagementFloor = 15,
                EngagementDropRange = (5, 20)
            }
        },
        new()
        {
            Name = "sadodere",
            Description =
                "Любит доминировать и манипулировать чувствами других. Получает удовольствие, дразня свою цель.",
            Emoji = "🩸",
            Color = Color.Parse("#be2edd"),
            BaseMoodVector = new MoodVector
            {
                Affection = (-3, 3),
                Engagement = (-3, 10),
                Mood = (-4, 8),
                Energy = (-1, 2)
            },
            Sensitivity = new ArchetypeSensitivity
            {
                AbsenceAffectionImpact = -2.0f, DaysAffectionBonus = 3.0f,
                AbsenceEngagementImpact = -6.0f, DaysEngagementBonus = 2.5f,
                AbsenceMoodImpact = -4.0f, DaysMoodBonus = 1.5f,
                AbsenceEnergyImpact = -1.0f, DaysEnergyBonus = 0.0f,
                DaysSaturation = 25,
                MessageSaturation = 1000,
                AbsenceTauHours = 18f,
                ResponseQuestionChance = 0.55f,
                EngagementDropChance = 0.35f,
                EngagementFloor = 20,
                EngagementDropRange = (5, 20)
            }
        }
    ];

    [ObservableProperty] private ArchetypeVM _selectedArchetype;

    #endregion

    
    #region MoodSettings

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AffectionLevel))]
    private int _affection;

    public AffectionType AffectionLevel => Affection switch
    {
        <= 25 => AffectionType.Bad,
        <= 50 => AffectionType.Normal,
        <= 75 => AffectionType.Good,
        _ => AffectionType.Love
    };

    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(EnergyLevel))]
    private int _energy;

    public EnergyType EnergyLevel => Energy switch
    {
        <= 20 => EnergyType.Low,
        <= 50 => EnergyType.Middle,
        _ => EnergyType.High
    };
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(EngagementLevel))]
    private int _engagement;

    public EngagementType EngagementLevel => Engagement switch
    {
        <= 30 => EngagementType.Indifferent,
        <= 70 => EngagementType.Balanced,
        _ => EngagementType.Interested
    };
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(MoodLevel))]
    private int _mood;

    public MoodType MoodLevel => Mood switch
    {
        <= 25 => MoodType.Bad,
        <= 65 => MoodType.Normal,
        _ => MoodType.Best
    };

    #endregion


    #region StatusSystem

    [ObservableProperty] private string? _userName;
    [ObservableProperty] private bool _isDating;

    [ObservableProperty] private DateTime _lastEngagementDrop;
    [ObservableProperty] private DateTime _lastEnergyDrop;

    #endregion
}