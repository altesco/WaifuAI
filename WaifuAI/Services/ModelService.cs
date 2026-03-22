using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Themes.Fluent;
using CommunityToolkit.Mvvm.Messaging;
using EmbedIO;

namespace WaifuAI.Services;

public static class ModelService
{
    public static async Task<string> StartWebServer(int port)
    {
        var baseDir = Path.Combine(AppContext.BaseDirectory, "WebAssets");
        var server = new WebServer(o => o
                .WithUrlPrefix($"http://localhost:{port}/")
                .WithMode(HttpListenerMode.EmbedIO))
            .WithStaticFolder("/", baseDir, false)
            .HandleHttpException((_, exception) =>
            {
                Debug.WriteLine($"EmbedIO Error (ignored): {exception.Message}");
                return Task.CompletedTask;
            });
        _ = server.RunAsync();
        while (server.State != WebServerState.Listening)
            await Task.Delay(50);
        string url = $"http://localhost:{port}/index.html";
        return url;
    }

    public static async Task<bool> WaitForResponce()
    {
        int jsStatus = 0;
        int attempts = 0;
        while (jsStatus == 0 && attempts < 20) 
        {
            var message = new EvaluateScriptMessage("return (typeof window.vrmApp !== 'undefined') ? 1 : 0;");
            await WeakReferenceMessenger.Default.Send(message);
            var responce = await message.Response;
            if (responce is Task<int> internalTask)
            {
                jsStatus = await internalTask;
                Console.WriteLine(jsStatus);
                if (jsStatus == 1)
                    return true;
            }
            await Task.Delay(200);
            attempts++;
            Console.WriteLine($"Попытка номер {attempts}");
        }
        return false;
    }

    public static void SetBackground()
    {
        var app = Application.Current;
        var theme = app?.Styles.OfType<FluentTheme>().FirstOrDefault();
        var variant = app?.ActualThemeVariant;
        if (theme is null ||
            variant is null ||
            !theme.Palettes.TryGetValue(variant, out var palette) || 
            palette is not { } colors)
            return;
        var color = colors.ChromeLow;
        string hexColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        string script = $"window.vrmApp.setBackground('{hexColor}');";
        WeakReferenceMessenger.Default.Send(new ExecuteScriptMessage(script));
    }
}