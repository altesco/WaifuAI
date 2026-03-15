using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using WaifuAI.Models;
using WaifuAI.Services;
using WebViewControl;
using Xilium.CefGlue;
using System.ComponentModel.DataAnnotations;

namespace WaifuAI.ViewModels
{
    public partial class MainVM : ObservableValidator
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
        [ObservableProperty] private MessageVM? _selectedMessage;
        [ObservableProperty] private string? _error;
        [ObservableProperty] private MessageVM? _replyMessage;


        [ObservableProperty] private bool _isSettingsOpen;

        

        public ObservableCollection<MessageVM> Chat { get; } = [];

        private readonly List<Message> _history = [
            new Message 
            { 
                Role = "system",
                Content = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prompt.txt"))
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
                _history.Add(message.MessageModel);
                query.Messages.AddRange(_history);
                Chat.Add(message);
                message.ReplyMessage = ReplyMessage;
                ReplyMessage = null;
                Question = string.Empty;
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
                    ReplyMessage = message.ReplyMessage;
                    _history.Remove(_history.Last());
                    return;
                }
                _history.Add(new Message
                {
                    Role = resultMessage.MessageModel.Role,
                    Content = resultMessage.MessageModel.Content
                });
                VoiceService.Say(
                    resultMessage.MessageModel.Content, 
                    source, 
                    SettingsVM.Instance.SelectedSource, 
                    SettingsVM.Instance.SelectedVoiceModel, 
                    SettingsVM.Instance.SelectedSpeaker, 
                    SettingsVM.Instance.Volume, 
                    SettingsVM.Instance.Bass, 
                    SettingsVM.Instance.Treble);
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
        
        // for some reason Cut() in TextBox is not working
        [RelayCommand]
        public void ManualCut(TextBox? textBox)
        {
            if (textBox == null || string.IsNullOrEmpty(textBox.SelectedText))
                return;
            textBox.Copy();
            Dispatcher.UIThread.Post(() =>
            {
                int start = Math.Min(textBox.SelectionStart, textBox.SelectionEnd);
                int length = Math.Abs(textBox.SelectionStart - textBox.SelectionEnd);
                string currentText = textBox.Text ?? string.Empty;
                textBox.Text = currentText.Remove(start, length);
                textBox.SelectionStart = start;
                textBox.SelectionEnd = start;
            }, DispatcherPriority.Background);
        }

        [RelayCommand]
        private void CopyMessageText(SelectableTextBlock? textBlock)
        {
            if (textBlock is null)
                return;
            textBlock.SelectAll();
            textBlock.Copy();
            textBlock.ClearSelection();
        }

        [RelayCommand]
        private void MakeQuote(string? text)
        {
            ReplyMessage = SelectedMessage;
            if (ReplyMessage is null)
                return;
            ReplyMessage.Quote = text;
            ReplyMessage.QuoteStart = Math.Min(ReplyMessage.SelectionStart, ReplyMessage.SelectionEnd);
            ReplyMessage.QuoteEnd = Math.Max(ReplyMessage.SelectionStart, ReplyMessage.SelectionEnd); 
            ReplyMessage.IsReplied = false;
        }
        
        [RelayCommand]
        private void MakeReply()
        {
            ReplyMessage = SelectedMessage;
            if (ReplyMessage is null)
                return;
            ReplyMessage.Quote = SelectedMessage?.MessageModel?.Content;
            ReplyMessage.IsReplied = true;
        }

        [RelayCommand]
        private void ReleaseReplyAndQuote()
        {
            if (ReplyMessage is null)
                return;
            ReplyMessage.Quote = null;
            ReplyMessage.QuoteStart = 0;
            ReplyMessage.QuoteEnd = 0;
            ReplyMessage.IsReplied = null;
            ReplyMessage = null;
        }

        [RelayCommand]
        private void Settings()
        {
            IsSettingsOpen = !IsSettingsOpen;
        }
    }
}
