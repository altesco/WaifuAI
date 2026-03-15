using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WaifuAI.Models;
using WaifuAI.ViewModels;

namespace WaifuAI.Services;

public static class QueryService
{
    private static readonly HttpClient _httpClient = new HttpClient() { Timeout = TimeSpan.FromMinutes(3) };
    private static HttpListener _httpListener;
    private static byte[] _latestAudioData;
    
    public static async Task<Message> DoServerQuery(QueryModel queryModel)
    {
        try
        {
            var query = JsonSerializer.Serialize(queryModel);
            var stringContent = new StringContent(query, Encoding.UTF8, "application/json");
            HttpResponseMessage answer = await _httpClient.PostAsync(
                $"http://{SettingsVM.Instance.IpAddress}:{SettingsVM.Instance.Port}/v1/chat/completions", stringContent);
            var json = await answer.Content.ReadAsStringAsync();
            Console.WriteLine(json);
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
                Role = "system",
                Content = "Ошибка: " + e.Message 
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
        queryModel.Model = SettingsVM.Instance.AiModel;
        var json = JsonSerializer.Serialize(queryModel);
        Console.WriteLine(json);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {SettingsVM.Instance.ApiKey}");
        try
        {
            var response = await _httpClient.PostAsync(SettingsVM.Instance.ApiUrl, content);
            var resJson = await response.Content.ReadAsStringAsync();
            Console.WriteLine(resJson);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<ResponceModel>(resJson, options);
            return result?.Choices[0].Message ?? new Message { Role = "system", Content = "Тишина..." };
        }
        catch (Exception ex)
        {
            return new Message 
            { 
                Role = "system",
                Content = "Ошибка: " + ex.Message 
            };
        }
    }

    public static List<string> GetModels(string language)
    {
        return language switch
        {
            "ru" => 
            [                
                "v5_cis_base", "v5_cis_ext", "v5_2_ru", "v5_1_ru", "v5_ru", 
                "v4_ru", "v3_1_ru", "ru_v3"
            ],
            "en" => ["v3_en", "v3_en_indic", "lj_v2"],
            "de" => ["v3_de", "thorsten_v2"],
            "es" => ["v3_es", "tux_v2"],
            "fr" => ["v3_fr", "gilles_v2"],
            _ => []
        };
    }
    
    public static async Task<List<string>> GetSpeakers(string model)
    {
        string url = $"http://127.0.0.1:5050/speakers?model_name={Uri.EscapeDataString(model)}";
        var json = await _httpClient.GetFromJsonAsync<SpeakerResponce>(url);
        return json?.Speakers ?? new List<string>();
    }
}

public class SpeakerResponce
{
    [JsonPropertyName("speakers")]
    public List<string> Speakers { get; set; }
}
