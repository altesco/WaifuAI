using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using WaifuAI.ViewModels;

namespace WaifuAI.Services;

public static class VoiceService
{
    public static int port = 5050;

    public static Process? PythonProcess;

    public static Dictionary<string, List<string>> LanguageModels { get; } = new()
    {
        ["ru"] = ["v5_cis_base", "v5_cis_ext", "v5_2_ru", "v5_1_ru", "v5_ru", "v4_ru", "v3_1_ru", "ru_v3"],
        ["en"] = ["v3_en", "v3_en_indic", "lj_v2"],
        ["de"] = ["v3_de", "thorsten_v2"],
        ["es"] = ["v3_es", "tux_v2"],
        ["fr"] = ["v3_fr", "gilles_v2"]
    };

    public static Dictionary<string, string> ModelsUrls { get; } = new()
    {
        ["v5_cis_base"] = "https://models.silero.ai/models/tts/ru/v5_cis_base.pt",
        ["v5_cis_ext"] = "https://models.silero.ai/models/tts/ru/v5_cis_ext.pt",
        ["v5_2_ru"] = "https://models.silero.ai/models/tts/ru/v5_2_ru.pt",
        ["v5_1_ru"] = "https://models.silero.ai/models/tts/ru/v5_1_ru.pt",
        ["v5_ru"] = "https://models.silero.ai/models/tts/ru/v5_ru.pt",
        ["v4_ru"] = "https://models.silero.ai/models/tts/ru/v4_ru.pt",
        ["v3_1_ru"] = "https://models.silero.ai/models/tts/ru/v3_1_ru.pt",
        ["ru_v3"] = "https://models.silero.ai/models/tts/ru/ru_v3.pt",
        ["v3_en"] = "https://models.silero.ai/models/tts/en/v3_en.pt",
        ["v3_en_indic"] = "https://models.silero.ai/models/tts/en/v3_en_indic.pt",
        ["lj_v2"] = "https://models.silero.ai/models/tts/en/v2_lj.pt",
        ["v3_de"] = "https://models.silero.ai/models/tts/de/v3_de.pt",
        ["thorsten_v2"] = "https://models.silero.ai/models/tts/de/v2_thorsten.pt",
        ["v3_es"] = "https://models.silero.ai/models/tts/es/v3_es.pt",
        ["tux_v2"] = "https://models.silero.ai/models/tts/es/v2_tux.pt",
        ["v3_fr"] = "https://models.silero.ai/models/tts/fr/v3_fr.pt",
        ["gilles_v2"] = "https://models.silero.ai/models/tts/fr/v2_gilles.pt"
    };

    public static void StartPythonServer()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string scriptPath = Path.Combine(baseDir, "say.py");
            string pythonExe = OperatingSystem.IsWindows()
                ? Path.Combine(baseDir, "python_runtime", "python.exe")
                : Path.Combine(baseDir, "venv", "bin", "python");

            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"-u \"{scriptPath}\"", // флаг -u для мгновенного вывода
                UseShellExecute = false,
                RedirectStandardOutput = true, // Перехватываем вывод
                RedirectStandardError = true,  // Перехватываем ошибки
                CreateNoWindow = true,
                WorkingDirectory = baseDir
            };
            PythonProcess = new Process { StartInfo = info, EnableRaisingEvents = true };
            PythonProcess.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.WriteLine($"[Python]: {e.Data}");
            };
            PythonProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.Error.WriteLine($"[Python ERROR]: {e.Data}");
            };
            PythonProcess.Start();
            PythonProcess.BeginOutputReadLine();
            PythonProcess.BeginErrorReadLine();
        }
        catch (Exception e)
        {
            Console.WriteLine(e + "\n" + e.Message);
        }
    }

    public static async Task WaitForPythonServerAsync(int timeoutSeconds = 20)
    {
        var startTime = DateTime.Now;
        while ((DateTime.Now - startTime).TotalSeconds < timeoutSeconds)
        {
            try 
            {
                var response = await ApiService.HttpClient.GetAsync("http://127.0.0.1:5050/health");
                if (response.IsSuccessStatusCode) 
                    return;
            }
            catch {  }
            await Task.Delay(1000);
        }
    }

    public static void Say(
        string text, 
        string service, 
        string modelName, 
        string language,
        string speaker, 
        double volume, 
        double pitch,
        double bass, 
        double treble)
    {
        var parsedResult = EmotionParser.ParseTextForEmotions(text);
        parsedResult.CleanText = parsedResult.CleanText.ToPhoneticCyrillic();
        Console.WriteLine(text);
        Console.WriteLine(parsedResult.CleanText);
        var dialogueData = new
        {
            cleanText = parsedResult.CleanText,
            emotions = parsedResult.Emotions.Select(e => new
            {
                name = e.Name,
                pos = e.OriginalPos
            }).ToList()
        };
        string jsonParams = JsonSerializer.Serialize(dialogueData);
        var modelPath = Path.Combine(SettingsVM.VoiceModelFolder, $"{modelName}.pt");
        string jsCall = $"window.say({jsonParams}, {pitch}, {port}, '{service}', '{modelPath}', '{language}', '{speaker}', {volume}, {bass}, {treble});";
        WeakReferenceMessenger.Default.Send(new ExecuteScriptMessage(jsCall));
    }

    public static string ToPhoneticCyrillic(this string text)
    {
        text = text.ToLower();
        var rules = new Dictionary<string, string>
        {
            { "question", "квесчен" },
            { "english", "инглиш" },
            { "because", "бикоз" },
            { "senpai", "семпай" },
            { "please", "плиз" },
            { "repeat", "репит" },
            { "could", "куд" },
            { "would", "вуд" },
            { "should", "шуд" },
            { "maybe", "мэйби" },
            { "sorry", "сорри" },
            { "hello", "хелоу" },
            { "think", "синк" },
            { "waifu", "вайфу" },
            { "kawaii", "кавай" },
            { "sensei", "сенсей" },
            { "what", "вот" },
            { "when", "вэн" },
            { "time", "тайм" },
            { "more", "мор" },
            { "need", "нид" },
            { "mean", "мин" },
            { "know", "ноу" },
            { "does", "даз" },
            { "this", "зис" },
            { "that", "зэт" },
            { "they", "зей" },
            { "have", "хэв" },
            { "baka", "бака" },
            { "chan", "тян" },
            { "how", "хау" },
            { "who", "ху" },
            { "why", "вай" },
            { "you", "ю" },
            { "are", "ар" },
            { "the", "зе" },
            { "yes", "йес" },
            { "boy", "бой" },
            { "kun", "кун" },
            { "san", "сан" },

            { "tion", "шн" },
            { "sion", "жн" },
            { "ture", "чер" },
            { "ight", "айт" },
            { "ough", "аф" },
            { "augh", "аф" },
            { "eigh", "эй" },
            { "ing", "инг" },
            
            { "shch", "щ" },
            { "sch", "щ" },
            { "tch", "ч" },
            { "ch", "ч" },
            { "sh", "ш" },
            { "zh", "ж" },
            { "th", "т" }, 
            { "ph", "ф" },
            { "wh", "в" },
            { "wr", "р" },
            { "kn", "н" },
            { "mb", "м" }, 
            { "qu", "кв" },
            { "ck", "к" },
            { "eau", "о" },
            { "ee", "и" },
            { "oo", "у" },
            { "ea", "и" },
            { "ie", "и" },
            { "ei", "эй" },
            { "ou", "ау" },
            { "ow", "ау" },
            { "ay", "эй" },
            { "ey", "эй" },
            { "oy", "ой" },
            { "oi", "ой" },
            { "aw", "о" },
            { "ew", "ю" },
            { "yo", "ё" },
            { "jo", "ё" },
            { "yu", "ю" },
            { "ju", "ю" },
            { "ya", "я" },
            { "ja", "я" },
            { "ts", "ц" },
            { "kh", "х" }
        };
        var letters = new Dictionary<string, string>
        {
            { "a", "а" }, { "b", "б" }, { "v", "в" }, { "g", "г" },
            { "d", "д" }, { "e", "е" }, { "z", "з" }, { "i", "и" },
            { "j", "й" }, { "k", "к" }, { "l", "л" }, { "m", "м" },
            { "n", "н" }, { "o", "о" }, { "p", "п" }, { "r", "р" },
            { "s", "с" }, { "t", "т" }, { "u", "у" }, { "f", "ф" },
            { "h", "х" }, { "c", "к" }, { "q", "к" }, { "w", "в" },
            { "x", "кс" }, { "y", "ы" }
        };
        StringBuilder sb = new StringBuilder(text);
        foreach (var rule in rules)
        {
            sb.Replace(rule.Key, rule.Value);
            sb.Replace(rule.Key.ToUpper(), rule.Value.ToUpper());
            string titleCase = char.ToUpper(rule.Key[0]) + rule.Key.Substring(1);
            sb.Replace(titleCase, char.ToUpper(rule.Value[0]).ToString());
        }
        string currentText = sb.ToString();
        sb.Clear();
        foreach (char c in currentText)
        {
            string l = c.ToString().ToLower();
            if (letters.ContainsKey(l))
            {
                bool isUpper = char.IsUpper(c);
                string replacement = letters[l];
                sb.Append(isUpper ? replacement.ToUpper() : replacement);
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
    
    public static async Task<List<string>> GetSpeakers(string modelPath, string language)
    {
        modelPath = Uri.EscapeDataString(modelPath);
        language = Uri.EscapeDataString(language);
        string url = $"http://127.0.0.1:5050/speakers?model_path={modelPath}&language={language}";
        var json = await ApiService.HttpClient.GetFromJsonAsync<SpeakerResponce>(url);
        return json?.Speakers ?? new List<string>();
    }
}

public class SpeakerResponce
{
    [JsonPropertyName("speakers")]
    public List<string> Speakers { get; set; }
}