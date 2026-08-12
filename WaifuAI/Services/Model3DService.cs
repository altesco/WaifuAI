using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Themes.Fluent;
using CommunityToolkit.Mvvm.Messaging;
using EmbedIO;
using WaifuAI.Models;

namespace WaifuAI.Services;

public static class Model3DService
{
    public static async Task<string> StartWebServer()
    {
        int port = GetFreePort();

        var baseDir = Path.Combine(AppContext.BaseDirectory, "WebAssets");
        var server = new WebServer(o => o
                .WithUrlPrefix($"http://127.0.0.1:{port}/")
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

        string url = $"http://127.0.0.1:{port}/index.html";
        return url;
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        // Принудительно отключаем задержку TIME_WAIT при закрытии
        listener.Server.LingerState = new LingerOption(true, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }


    public static async Task<bool> WaitForResponce()
    {
        int jsStatus = 0;
        int attempts = 0;
        while (jsStatus == 0 && attempts < 20) 
        {
            var message = new EvaluateScriptMessage<int>("return (typeof window.vrmApp !== 'undefined') ? 1 : 0;");
            jsStatus = await WeakReferenceMessenger.Default.Send(message);
            
            Console.WriteLine(jsStatus);
            
            if (jsStatus == 1)
                return true;

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

    public static void SetCamera(CameraVariant camera)
    {
        var view = camera.ToString().ToLower();
        var script = $"window.vrmApp.setCameraMode('{view}');";
        WeakReferenceMessenger.Default.Send(new ExecuteScriptMessage(script));
    }
}