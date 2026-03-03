using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using EmbedIO;

namespace WaifuAI.Services;

public static class ModelService
{
    public static void StartWebServer(int port, out string url)
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
        server.RunAsync();
        url = $"http://localhost:{port}/index.html";
    }
}