using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
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

    public static void Say(string text, WebView source, string service, string speaker)
    {
        var parsedResult = EmotionParser.ParseTextForEmotions(text);
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
        string jsCall = $"window.say({jsonParams}, 1.1, {port}, '{service}', '{speaker}');";
        source.ExecuteScript(jsCall);
    }
}