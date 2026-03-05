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
        [ObservableProperty] private string? _error;

        public ObservableCollection<MessageVM> Chat { get; } = 
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
        private async Task Query(object param)
        {
            if (param is not WebView source)
                return;
            try
            {
                CloseErrorMessage();
                var query = new QueryModel();
                var message = new MessageVM
                {
                    MessageModel = new Message { Role = "user", Content = Question },
                    Time = DateTime.Now.ToString("HH:mm")
                };
                Chat.Add(message);
                Question = string.Empty;
                query.Messages.AddRange(_history);
                var tempMessage = new MessageVM
                {
                    MessageModel = new Message { Role = "temp" }
                };
                Chat.Add(tempMessage);
                MessageVM resultMessage = new MessageVM();
                if (SettingsVM.Instance.IsServerQuery)
                    resultMessage.MessageModel = await QueryService.DoServerQuery(query);
                else
                    resultMessage.MessageModel = await QueryService.DoProviderQuery(query);
                Chat.Remove(tempMessage);
                if (resultMessage.MessageModel.Role == "system")
                {
                    Question = message.MessageModel.Content;
                    Error = resultMessage.MessageModel.Content;
                    message.IsFailed = true;
                    return;
                }
                _history.Add(message.MessageModel);
                _history.Add(resultMessage.MessageModel);
                VoiceService.Say(resultMessage.MessageModel.Content, source);
                resultMessage.MessageModel.Content = EmotionParser.CleanText(resultMessage.MessageModel.Content);
                resultMessage.Time = DateTime.Now.ToString("HH:mm");
                Chat.Add(resultMessage);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Ошибка в Query: " + e.Message);
            }
        }

        [RelayCommand]
        private void CloseErrorMessage()
        {
            Error = null;
        }
    }
}
