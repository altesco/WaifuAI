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
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
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

    public static readonly string PromptsPath = Path.Combine(AppDirectory, "Prompts");

    private SettingsModel SettingsModel { get; set; }

    [ObservableProperty] private bool _isLoading;

    public async Task Load()
    {
        IsLoading = true;
        if (File.Exists(FilePath))
        {
            var json = await File.ReadAllTextAsync(FilePath);
            SettingsModel = JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();
        }
        else
            SettingsModel = new SettingsModel();

        // AI Settings
        Port = SettingsModel.Port; Console.WriteLine("порт есть");
        IpAddress = SettingsModel.IpAddress; Console.WriteLine("ip есть");
        ApiKey = SettingsModel.ApiKey; Console.WriteLine("апи ключ есть");
        ApiUrl = SettingsModel.ApiUrl; Console.WriteLine("апи урл есть");
        AiModel = SettingsModel.AIModel; Console.WriteLine("модель ии есть");
        IsServerQuery = SettingsModel.IsServerQuery; Console.WriteLine("бул есть");
        
        // General Settings
        SelectedTheme = SettingsModel.Theme; Console.WriteLine("тема есть");
        SelectedColor = SettingsModel.AccentColor; Console.WriteLine("цвет есть");
        RefreshFonts(); Console.WriteLine("шрифт есть");
        RefreshMonoFonts(); Console.WriteLine("моношрифт есть");
        SelectedAppLanguage = SettingsModel.AppLanguage; Console.WriteLine("язык приложения есть");
        SelectedLanguage = SettingsModel.Language; Console.WriteLine("язык ии есть");

        // Voice Settings
        SelectedSource = SettingsModel.Source; Console.WriteLine("источник звука есть");
        SelectedVoiceModel = VoiceService.LanguageModels[SelectedLanguage].Contains(SettingsModel.VoiceModel) ?
            SettingsModel.VoiceModel : VoiceService.LanguageModels[SelectedLanguage][0];
        Console.WriteLine("модель звука есть");
        SelectedSpeaker = SettingsModel.Speaker; Console.WriteLine("спикер есть");
        Volume = SettingsModel.Volume; Console.WriteLine("звук есть");
        Bass = SettingsModel.Bass; Console.WriteLine("бас есть");
        Treble = SettingsModel.Treble; Console.WriteLine("требл есть");
        Pitch = SettingsModel.Pitch; Console.WriteLine("питч есть");

        // 3D Model Settings
        if (Directory.Exists(SettingsModel.Model3DFolder))
            Model3DFolder = SettingsModel.Model3DFolder;
        else
            Directory.CreateDirectory(Model3DFolder);
        RefreshModels3D();
        Console.WriteLine("модель 3д есть");

        // Personality
        SelectedArchetype = 
            Archetypes.Find(x => x.Name == SettingsModel.SelectedArchetype) ?? 
            Archetypes[0];

        // Prompts
        Directory.CreateDirectory(PromptsPath);
        foreach (var archetype in Archetypes)
        {
            var promptPath = Path.Combine(PromptsPath, $"{archetype.Name}.txt");
            if (!File.Exists(promptPath))
                File.Create(promptPath);
            else
                archetype.Prompt = 
                    await File.ReadAllTextAsync(promptPath);
        }

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

        // 3D Model Settings
        SettingsModel.SelectedModel3D = SelectedModel3D;       
        SettingsModel.Model3DFolder = Model3DFolder;

        // Personality Settings
        SettingsModel.SelectedArchetype = SelectedArchetype.Name;

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(SettingsModel, options);
        File.WriteAllText(FilePath, json);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (IsLoading)
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

    #region GeneralSettings
    
    [ObservableProperty] private int _selectedTheme;

    [ObservableProperty] 
    [NotifyDataErrorInfo]
    [RegularExpression("^#([A-Fa-f0-9]{3}|[A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$")]
    private string _selectedColor;

    partial void OnSelectedColorChanged(string value)
    {
        var app = Application.Current;
        var theme = app?.Styles.OfType<FluentTheme>().FirstOrDefault();
        Console.WriteLine("зашел");
        if (HasErrors || app is null || theme is null || !Color.TryParse(value, out var color))
            return;
        Console.WriteLine("ошибок нет, тема не налл, значение распарсил");
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
        if (IsLoading || string.IsNullOrEmpty(value))
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
        _ = RefreshVoiceModelInfo();
    }

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

    #region PersonalitySettings

    public List<ArchetypeVM> Archetypes { get; } =
    [
        new() 
        { 
            Name = "tsundere", 
            Description = "Колючая снаружи, но мягкая и заботливая внутри. Проявляет симпатию через напускную грубость.", 
            Emoji = "🔥", 
            Color = Color.Parse("#ff5c5c")
        },
        new() 
        { 
            Name = "kuudere", 
            Description = "Хладнокровная, молчаливая и внешне безэмоциональная. Скрывает глубокие чувства за маской апатии.", 
            Emoji = "🧊", 
            Color = Color.Parse("#0008ff")
        },
        new() 
        { 
            Name = "dandere", 
            Description = "Крайне стеснительная и молчаливая личность. Раскрывается только в узком кругу тех, кому доверяет.", 
            Emoji = "😳", 
            Color = Color.Parse("#544dc2")
        },
        new() 
        { 
            Name = "deredere", 
            Description = "Воплощение чистой любви и оптимизма. Всегда искренняя, теплая и энергично заботится об окружающих.", 
            Emoji = "💓", 
            Color = Color.Parse("#ff4b8a")
        },
        new() 
        { 
            Name = "genki", 
            Description = "Неиссякаемый источник энергии. Жизнерадостная, активная и всегда готова вдохновлять на подвиги.", 
            Emoji = "🌞", 
            Color = Color.Parse("#dfa017")
        },
        new() 
        { 
            Name = "yandere", 
            Description = "Одержимая и пугающе преданная. Готова на любые крайности ради того, чтобы объект любви принадлежал только ей.", 
            Emoji = "🔪", 
            Color = Color.Parse("#cb0e0e")
        },
        new() 
        { 
            Name = "teasedere", 
            Description = "Мастер подколов и легкого кокетства. Обожает смущать собеседника и проявляет чувства через дразнилки.", 
            Emoji = "❤️‍🔥", 
            Color = Color.Parse("#ff9431")
        },
        new() 
        { 
            Name = "dorodere", 
            Description = "Милая и добрая на первый взгляд, но хранит внутри затаенную обиду или жестокую сторону.", 
            Emoji = "⚫", 
            Color = Colors.SlateGray
        },
        new() 
        { 
            Name = "utsudere", 
            Description = "Меланхоличная личность, склонная к грусти и депрессивным настроениям из-за тяжелого прошлого.", 
            Emoji = "💧", 
            Color = Color.Parse("#0876d6")
        },
        new() 
        { 
            Name = "bakadere", 
            Description = "Наивная, неуклюжая и очень открытая. Не умеет скрывать чувства и часто попадает в неловкие ситуации.", 
            Emoji = "🐔", 
            Color = Colors.Brown
        },
        new() 
        { 
            Name = "darudere", 
            Description = "Ленивая и слегка отстраненная. Предпочитает покой и отдых любым активным действиям.", 
            Emoji = "💤", 
            Color = Color.Parse("#1d6bc5")
        },
        new() 
        { 
            Name = "hinedere", 
            Description = "Циничная и высокомерная снаружи, но способна измениться, если найдет кого-то достойного доверия.", 
            Emoji = "🚬", 
            Color = Color.Parse("#318eb0")
        },
        new() 
        { 
            Name = "sadodere", 
            Description = "Любит доминировать и манипулировать чувствами других. Получает удовольствие, дразня свою цель.", 
            Emoji = "🩸", 
            Color = Color.Parse("#be2edd")
        }
    ];
    [ObservableProperty] private ArchetypeVM _selectedArchetype;

    #endregion
}