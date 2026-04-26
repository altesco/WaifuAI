using System;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using WaifuAI.Models;
using WaifuAI.ViewModels;

namespace WaifuAI.Services;

public static class QueryService
{   
    public static async Task<Message> DoServerQuery(QueryModel queryModel)
    {
        try
        {
            var query = JsonSerializer.Serialize(queryModel);
            var stringContent = new StringContent(query, Encoding.UTF8, "application/json");
            HttpResponseMessage answer = await ApiService.HttpClient.PostAsync(
                $"http://{SettingsVM.Instance.IpAddress}:{SettingsVM.Instance.Port}/v1/chat/completions", stringContent);
            var json = await answer.Content.ReadAsStringAsync();
            Console.WriteLine(json);
            if (!answer.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error", out var errorElement) &&
                    errorElement.TryGetProperty("message", out var messageElement))
                {
                    return new Message
                    {
                        Role = "system", 
                        Content = $"Ошибка {(int)answer.StatusCode}: {messageElement.GetString()}"
                    };
                }
                return new Message { Role = "system", Content = $"Ошибка {(int)answer.StatusCode}" };
            }
            var model = JsonSerializer.Deserialize<ResponceModel>(json);
            if (model == null)
                return new Message 
                { 
                    Role = "assistant", 
                    Content = "wtf something is wrong" 
                };
            var message = model.Choices[0].Message;
            message.Time = DateTime.Now;
            return message;
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

    public static async Task<Message> DoProviderQuery(QueryModel queryModel)
    {
        queryModel.Model = SettingsVM.Instance.AiModel;
        var serializeOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(queryModel, serializeOptions);
        Console.WriteLine(json);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        ApiService.HttpClient.DefaultRequestHeaders.Clear();
        ApiService.HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {SettingsVM.Instance.ApiKey}");
        try
        {
            var response = await ApiService.HttpClient.PostAsync(SettingsVM.Instance.ApiUrl, content);
            var resJson = await response.Content.ReadAsStringAsync();
            Console.WriteLine(resJson);
            if (!response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(resJson);
                if (doc.RootElement.TryGetProperty("error", out var errorElement) &&
                    errorElement.TryGetProperty("message", out var messageElement))
                {
                    return new Message
                    {
                        Role = "system", 
                        Content = $"Ошибка {(int)response.StatusCode}: {messageElement.GetString()}"
                    };
                }
                return new Message { Role = "system", Content = $"Ошибка {(int)response.StatusCode}" };
            }
            var deserializeOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<ResponceModel>(resJson, deserializeOptions);
            var message = result?.Choices[0].Message ?? 
                          new Message { Role = "assistant", Content = "wtf something is wrong" };
            message.Time = DateTime.Now;
            return message;
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
}
