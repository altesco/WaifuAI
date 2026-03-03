using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using WaifuAI.Models;
using WaifuAI.Services;
using WebViewControl;

namespace WaifuAI.ViewModels
{
    public partial class MainVM : ObservableObject
    {
        public MainVM()
        {
            SettingsVM.Instance.Load();
            QueryService.StartHttpServer(); 
            ModelService.StartWebServer(12347, out var url);
            VoiceService.StartPythonServer();
            _webAddress = url;
        }

        [ObservableProperty] private string _question = string.Empty;
        [ObservableProperty] private string _webAddress;

        public ObservableCollection<Message> Messages { get; } = 
            [
                /*new Message { Role = "user", Content = "привет" },
                new Message { Role = "assistant", Content = "привет, как дела? у меня вот нормально. Че делаешь? мне вот надо щас напиздеть в одном сообщении много слов чтобы хватило для wrap" }
            */
            ];

        private readonly List<Message> _history = [
            new Message 
            { 
                Role = "system", 
                Content = File.ReadAllText(Path.Combine(".", "promt.txt")) 
            }];

        [RelayCommand]
        public async Task Query(object param)
        {
            if (param is not WebView source)
                return;
            try
            {
                var query = new QueryModel();
                var message = new Message
                {
                    Role = "user", 
                    Content = Question,
                    Time = DateTime.Now.ToString("HH:mm"),
                };
                _history.Add(message);
                Messages.Add(message);
                Question = string.Empty;
                query.Messages.AddRange(_history);
                var tempMessage = new Message { Role = "temp" };
                Messages.Add(tempMessage);
                Message resultMessage;
                if (SettingsVM.Instance.IsServerQuery)
                    resultMessage = await QueryService.DoServerQuery(query);
                else
                    resultMessage = await QueryService.DoProviderQuery(query);
                resultMessage.Time = DateTime.Now.ToString("HH:mm");
                _history.Add(resultMessage);
                Messages.Remove(tempMessage);
                VoiceService.Say(resultMessage.Content, source);
                resultMessage.Content = EmotionParser.CleanText(resultMessage.Content);
                Messages.Add(resultMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Ошибка в Query: " + ex.Message);
            }
        }
    }
}
