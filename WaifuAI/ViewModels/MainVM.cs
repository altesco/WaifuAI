using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using WaifuAI.Models;
using WaifuAI.Services;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Extensions;

namespace WaifuAI.ViewModels
{
    public partial class MainVM : ObservableValidator
    {
        public MainVM()
        {
            _ = InitializeAsync();
            OnPropertyChanged(nameof(IsWindows));
        }

        private async Task InitializeAsync()
        {
            IsInitializing = true;
            InitializingMessage = "Загрузка данных...";
            await SettingsVM.Instance.Load();
            InitializingMessage = "Запуск веб-сервера...";
            WebAddress = await ModelService.StartWebServer(12347);
            await ModelService.WaitForResponce();
            await SettingsVM.Instance.InitializeModel3D();
            InitializingMessage = "Запуск звукового сервера...";
            VoiceService.StartPythonServer();
            await VoiceService.WaitForPythonServerAsync();
            await SettingsVM.Instance.InitializeSpeakers();
            InitializingMessage = "Создание векторного генератора...";
            await MessageParser.CreateVectorGenerator();
            InitializingMessage = "Загрузка базы знаний...";
            await DatabaseService.InitializeDatabase(KnowledgeBasePath);
            var records = await DatabaseService.GetRecordsAsync();
            foreach (var record in records)
                 KnowledgeBase.Add(record);
            IsInitializing = false;
            KnowledgeBase.CollectionChanged += OnKnowledgeBaseChanged;
        }

        private static readonly string KnowledgeBasePath =
            Path.Combine(SettingsVM.AppDirectory, "knowledge_base.json");

        public bool IsWindows => OperatingSystem.IsWindows();

        [ObservableProperty] private bool _isInitializing;
        [ObservableProperty] private string _initializingMessage;
        [ObservableProperty] private string _webAddress;
        [ObservableProperty] private string _question = string.Empty;
        [ObservableProperty] private MessageVM? _selectedMessage;
        [ObservableProperty] private string? _error;
        [ObservableProperty] private MessageVM? _replyMessage;
        [ObservableProperty] private bool _isSettingsOpen;

        public ObservableCollection<MessageVM> Chat { get; } = [];
        public ObservableCollection<KnowledgeRecord> KnowledgeBase { get; } = [];

        private void OnKnowledgeBaseChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (KnowledgeRecord record in e.NewItems)
                    DatabaseService.SaveRecordAsync(record);
            if (e.OldItems != null)
                foreach (KnowledgeRecord record in e.OldItems)
                    DatabaseService.RemoveRecordAsync(record);
        }

        private readonly List<Message> _history = [
            new Message 
            { 
                Role = "system",
                Content = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prompt.txt"))
            }];

        private async Task<Message> GetSystemPrompt()
        {
            if (_history.Count <= 0)
                return new Message();
            var text = _history[0].Content;
            var message = new Message
            {
                Role = "system",
                Content = text
            };
            var header = "[Knowledge Records]";
            var embedding = 
                await MessageParser.VectorGenerator.GenerateEmbeddingAsync(Question);
            var recordsToAdd = KnowledgeBase
                .Select(r => new { 
                    Record = r, 
                    Score = embedding.Vector.CosineSimilarity(r.Vector) 
                })
                .OrderByDescending(x => x.Score)
                .Take(5)
                .Select(x => x.Record)
                .ToList();
            if (recordsToAdd.Count <= 0)
                return message;
            message.Content += $"\n{header}\n";
            foreach (var record in recordsToAdd)
                message.Content += $"{record.Key}: {record.Value}\n";
            return message;
        }

        [RelayCommand]
        private async Task Query()
        {
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
                var systemPrompt = await GetSystemPrompt();
                query.Messages.Add(systemPrompt);
                query.Messages.AddRange(_history.Skip(1));
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
                var messageText = resultMessage.MessageModel.Content;
                _history.Add(new Message
                {
                    Role = resultMessage.MessageModel.Role,
                    Content = messageText
                });
                VoiceService.Say(
                    messageText, 
                    SettingsVM.Instance.SelectedSource, 
                    SettingsVM.Instance.SelectedVoiceModel,
                    SettingsVM.Instance.SelectedLanguage,                    
                    SettingsVM.Instance.SelectedSpeaker, 
                    SettingsVM.Instance.Volume, 
                    SettingsVM.Instance.Pitch,
                    SettingsVM.Instance.Bass, 
                    SettingsVM.Instance.Treble);
                resultMessage.MessageModel.Content = MessageParser.GetCleanText(messageText);
                resultMessage.Time = DateTime.Now.ToString("HH:mm");
                Chat.Add(resultMessage);
                await MessageParser.ParseTextForKnowledgeUpdates(messageText, KnowledgeBase);
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

        [RelayCommand]
        private async Task ToggleFavoriteFact(object? args)
        {
            if (args is not KnowledgeRecord record)
                return;
            await DatabaseService.UpdateFavoriteAsync(record.Id, record.IsFavorite);
        }

        [ObservableProperty] private bool _isMaximized;
    }
}
