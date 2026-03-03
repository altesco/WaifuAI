using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WaifuAI.Models;
using WaifuAI.ViewModels;

namespace WaifuAI.Services
{
    internal static class QueryService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static HttpListener _httpListener;
        private static byte[] _latestAudioData;
        
        public static async Task<Message> DoServerQuery(QueryModel queryModel)
        {
            Console.WriteLine("я тут");
            try
            {
                var query = JsonSerializer.Serialize(queryModel);
                var stringContent = new StringContent(query, Encoding.UTF8, "application/json");
                HttpResponseMessage answer = await _httpClient.PostAsync(
                    $"http://{SettingsVM.Instance.IpAddress}:{SettingsVM.Instance.Port}/v1/chat/completions", stringContent);
                var json = await answer.Content.ReadAsStringAsync();
                var model = JsonSerializer.Deserialize<ResponceModel>(json);
                if (model == null)
                    return new Message 
                    { 
                        Role = "assistant", 
                        Content = "wtf something is wrong" 
                    };
                return model.Choices[0].Message;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return new Message 
                { 
                    Role = "assistant", 
                    Content = e.Message
                };
            }
        }

        public static void StartHttpServer()
        {
            if (_httpListener != null && _httpListener.IsListening)
                return;
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add("http://127.0.0.1:12345/");
            _httpListener.Start();
            Task.Run(async () =>
            {
                while (_httpListener.IsListening)
                {
                    var context = await _httpListener.GetContextAsync();
                    var response = context.Response;
                    if (_latestAudioData != null)
                    {
                        response.ContentType = "audio/mpeg";
                        response.ContentLength64 = _latestAudioData.Length;
                        await response.OutputStream.WriteAsync(_latestAudioData, 0, _latestAudioData.Length);
                    }
                    else
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.OutputStream.Close();
                }
            });
        }

        public static async Task<Message> DoProviderQuery(QueryModel queryModel)
        {
            queryModel.Model = SettingsVM.Instance.Model;
            var json = JsonSerializer.Serialize(queryModel);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {SettingsVM.Instance.ApiKey}");
            try
            {
                var response = await _httpClient.PostAsync(SettingsVM.Instance.ApiUrl, content);
                var resJson = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<ResponceModel>(resJson, options);
                return result?.Choices[0].Message ?? new Message { Content = "Тишина..." };
            }
            catch (Exception ex)
            {
                return new Message 
                { 
                    Content = "Ошибка: " + ex.Message 
                };
            }
        }
    }
}
