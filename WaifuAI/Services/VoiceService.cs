using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Cyrillic.Convert;
using WaifuAI.ViewModels;
using WebViewControl;

namespace WaifuAI.Services;

public static class VoiceService
{
    public static int port = 5050;
    
    public static void StartPythonServer()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string scriptPath = Path.Combine(baseDir, "say.py");
            string venvPython = Path.Combine(baseDir, "venv", "bin", "python");
            string pythonExe = File.Exists(venvPython) ? venvPython : "python";
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = baseDir
            };
            Process.Start(info);
        }
        catch (Exception e)
        {
            Console.WriteLine(e + "\n" + e.Message);
        }
    }

    public static void Say(
        string text, 
        WebView source, 
        string service, 
        string model, 
        string speaker, 
        double volume, 
        double bass, 
        double treble)
    {
        var parsedResult = EmotionParser.ParseTextForEmotions(text);
        parsedResult.CleanText = parsedResult.CleanText.ToPhoneticCyrillic();
        System.Console.WriteLine(parsedResult.CleanText);
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
        string jsCall = $"window.say({jsonParams}, 1.1, {port}, '{service}', '{model}', '{speaker}', {volume}, {bass}, {treble});";
        source.ExecuteScript(jsCall);
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
}