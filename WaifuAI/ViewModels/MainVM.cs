using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using WaifuAI.Models;
using WaifuAI.Services;
using CommunityToolkit.Mvvm.Messaging;
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Extensions;
using SharpToken;

namespace WaifuAI.ViewModels;

public partial class MainVM : ObservableValidator
{
    public MainVM()
    {
        _ = InitializeAsync();
        OnPropertyChanged(nameof(IsWindows));
    }

    private async Task InitializeAsync()
    {
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
        InitializingMessage = "Подготовка лингвистических модулей...";
        await Task.Run(() => _encoder = GptEncoding.GetEncoding("cl100k_base"));
        InitializingMessage = "Загрузка базы знаний...";
        await DatabaseService.InitializeDatabases();
        var records = 
            await DatabaseService.KnowledgeDb.Table<KnowledgeRecord>().ToListAsync();
        foreach (var record in records)
            KnowledgeBase.Add(record);
        InitializingMessage = "Загрузка истории чата...";
        var messages = await DatabaseService.HistoryDb.Table<Message>()
            .OrderBy(m => m.Time)
            .ToListAsync();
        foreach (var msg in messages)
        {
            _history.Add(msg);
            Chat.Add(new MessageVM(msg));
        }
        var messageMap = Chat.ToDictionary(m => m.MessageModel.Id);
        foreach (var msg in Chat)
        {
            if (msg.MessageModel.ReplyMessageId == null ||
                !messageMap.TryGetValue(msg.MessageModel.ReplyMessageId.Value, out var replyMsg))
                continue;
            msg.ReplyMessage = replyMsg;
            replyMsg.ReplyingMessages.Add(msg);
        }
        SettingsVM.Instance.IsAppInitializing = false;
        KnowledgeBase.CollectionChanged += OnKnowledgeBaseChanged;
        SelectedMessages.CollectionChanged += OnSelectedMessagesChanged;
    }

    private static GptEncoding _encoder;

    public bool IsWindows => OperatingSystem.IsWindows();
    [ObservableProperty] private string _initializingMessage;
    [ObservableProperty] private string _webAddress;
    [ObservableProperty] private string _question = string.Empty;
    [ObservableProperty] private int? _tokens;
    [ObservableProperty] private MessageVM? _selectedMessage;
    public ObservableCollection<MessageVM> SelectedMessages { get; } = [];
    [ObservableProperty] private bool _isMultiSelect;
    [ObservableProperty] private string? _error;

    [ObservableProperty] private MessageVM? _replyMessage;    
    // у сообщения которое готовится
    [ObservableProperty] private string? _quote;
    [ObservableProperty] private int _quoteStart;
    [ObservableProperty] private int _quoteEnd;

    [ObservableProperty] private bool _isSettingsOpen;
    [ObservableProperty] private bool _isPromptEditorOpen;
    [ObservableProperty] private bool _isDeletingDialogOpen;
    public ObservableCollection<MessageVM> Chat { get; } = [];
    public ObservableCollection<KnowledgeRecord> KnowledgeBase { get; } = [];

    partial void OnQuestionChanged(string value)
    {
        var tokens = _encoder.Encode(value);
        Tokens = tokens.Count == 0
            ? null
            : tokens.Count;
    }

    private void OnKnowledgeBaseChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (KnowledgeRecord record in e.NewItems)
                DatabaseService.KnowledgeDb.InsertOrReplaceAsync(record);
        if (e.OldItems != null)
            foreach (KnowledgeRecord record in e.OldItems)
                DatabaseService.KnowledgeDb.DeleteAsync(record);
    }

    private void OnSelectedMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (IsMultiSelect && SelectedMessages.Count == 0)
            Dispatcher.UIThread.Post(() => IsMultiSelect = false);
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
        var archetypePrompt = SettingsVM.Instance.SelectedArchetype.Prompt;
        var text = $"{archetypePrompt}\n\n{_history[0].Content}";
        var message = new Message
        {
            Role = "system",
            Content = $"[Current DateTime: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss, dddd")}]\n" +
                      $"Это текущее время и дата семпая. Поэтому это и твое текущее время и дата тоже.\n\n" +
                      $"{text}"
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
        message.Content += $"\n\n{header}\n";
        foreach (var record in recordsToAdd)
            message.Content += $"{record.Key}: {record.Value}\n";
        return message;
    }

    [RelayCommand]
    private async Task Query()
    {
        var timestamp = DateTime.Now;
        try
        {
            CloseErrorMessage();
            var query = new QueryModel();
            var message = new MessageVM(messageModel: new()
            {
                Role = "user",
                CleanText = Question,
                Content = $"[Sent at: {timestamp.ToString("yyyy-MM-dd HH:mm:ss, dddd")}]\n",
                Time = timestamp,
                Tokens = Tokens ?? 0
            });

            if (ReplyMessage?.IsReplied == true)
                message.MessageModel.Content +=
                    $"[Replying to the {ReplyMessage.MessageModel.Role}'s message: '{Quote}']\n\n" +
                    $"{Question}";
            else if (ReplyMessage?.IsReplied == false)
                message.MessageModel.Content +=
                    $"[Replying to the {ReplyMessage.MessageModel.Role}'s quote: '{Quote}']\n\n" +
                    $"{Question}";
            else
                message.MessageModel.Content += $"\n{Question}";
            _history.Add(message.MessageModel);

            var systemPrompt = await GetSystemPrompt();
            query.Messages.Add(systemPrompt);
            query.Messages.AddRange(_history.Skip(1));
            Chat.Add(message);

            ReplyMessage?.ReplyingMessages.Add(message);
            message.ReplyMessage = ReplyMessage;
            message.Quote = Quote;
            message.QuoteStart = QuoteStart;
            message.QuoteEnd = QuoteEnd;
            Quote = null;
            QuoteStart = 0;
            QuoteEnd = 0;
            ReplyMessage = null;
            Question = string.Empty;

            var tempMessage = new MessageVM(
                messageModel: new Message { Role = "temp" }
            );
            Chat.Add(tempMessage);

            var messageModel = SettingsVM.Instance.IsServerQuery
                ? await QueryService.DoServerQuery(query)
                : await QueryService.DoProviderQuery(query);
            var resultMessage = new MessageVM(messageModel);
            Chat.Remove(tempMessage);

            if (resultMessage.MessageModel.Role == "system")
            {
                Question = message.MessageModel.CleanText;
                Error = resultMessage.MessageModel.Content;
                message.IsFailed = true;
                ReplyMessage = message.ReplyMessage;
                _history.Remove(_history.Last());
                return;
            }

            var messageText = resultMessage.MessageModel.Content;
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

            resultMessage.MessageModel.CleanText = MessageParser.GetCleanText(messageText);
            resultMessage.MessageModel.Content =
                $"[Sent at: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss, dddd")}]\n" +
                resultMessage.MessageModel.Content;

            _history.Add(resultMessage.MessageModel);
            await DatabaseService.HistoryDb.InsertOrReplaceAsync(message.MessageModel);
            await DatabaseService.HistoryDb.InsertOrReplaceAsync(resultMessage.MessageModel);
            Chat.Add(resultMessage);
            
            await MessageParser.ParseTextForKnowledgeUpdates(messageText, KnowledgeBase);
        }
        catch (Exception e)
        {
            Console.WriteLine("Ошибка в Query: " + e.Message);
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
        Quote = text;
        QuoteStart = Math.Min(ReplyMessage.SelectionStart, ReplyMessage.SelectionEnd);
        QuoteEnd = Math.Max(ReplyMessage.SelectionStart, ReplyMessage.SelectionEnd);
        ReplyMessage.IsReplied = false;
    }

    [RelayCommand]
    private void MakeReply()
    {
        ReplyMessage = SelectedMessage;
        if (ReplyMessage is null)
            return;
        Quote = SelectedMessage?.MessageModel.CleanText;
        ReplyMessage.IsReplied = true;
    }

    [RelayCommand]
    private void ReleaseReplyAndQuote()
    {
        if (ReplyMessage is null)
            return;
        Quote = null;
        QuoteStart = 0;
        QuoteEnd = 0;
        ReplyMessage.IsReplied = null;
        ReplyMessage = null;
    }

    [RelayCommand]
    private void ToggleMultiSelect()
    {
        if (IsMultiSelect)
            SelectedMessages.Clear();
        IsMultiSelect = !IsMultiSelect;
    }

    [RelayCommand]
    private void DeletingDialog()
    {
        WeakReferenceMessenger.Default.Send(new SnapshotMessage(!IsDeletingDialogOpen));
        IsDeletingDialogOpen = !IsDeletingDialogOpen;
    }
    
    [RelayCommand]
    private void DeleteMessage()
    {
        var messagesToDelete = SelectedMessages.ToList();
        foreach (var msg in messagesToDelete)
        {
            _history.Remove(msg.MessageModel);

            foreach (var replyingMsg in msg.ReplyingMessages)
            {
                replyingMsg.MessageModel.Content =
                    $"[Sent at: {replyingMsg.MessageModel.Time.ToString("yyyy-MM-dd HH:mm:ss, dddd")}]\n\n" +
                    replyingMsg.MessageModel.CleanText;
                replyingMsg.ReplyMessage = null;
            }
            msg.ReplyMessage?.ReplyingMessages.Remove(msg);

            DatabaseService.HistoryDb.DeleteAsync(msg.MessageModel);
            Chat.Remove(msg);

            if (msg.MessageModel.Id == ReplyMessage?.MessageModel.Id)
                ReleaseReplyAndQuote();
        }
        SelectedMessages.Clear();
        SelectedMessage = null;
        IsMultiSelect = false;
        IsDeletingDialogOpen = false;
    }

    [RelayCommand]
    private void ScrollToMessage(object source)
    {
        if (source is not MessageVM msg || msg.ReplyMessage is null)
            return;
        int sourceIndex = Chat.IndexOf(msg);
        int replyIndex = Chat.IndexOf(msg.ReplyMessage);
        WeakReferenceMessenger.Default.Send(new ScrollMessage((sourceIndex, replyIndex)));
    }

    [RelayCommand]
    private void Settings()
    {
        WeakReferenceMessenger.Default.Send(new SnapshotMessage(!IsSettingsOpen));
        IsSettingsOpen = !IsSettingsOpen;
    }

    private string? _oldPrompt;

    [RelayCommand]
    private void PromptEditor()
    {
        if (!IsPromptEditorOpen)
            _oldPrompt = SettingsVM.Instance.SelectedArchetype.Prompt;
        else if (_oldPrompt != null)
        {
            SettingsVM.Instance.SelectedArchetype.Prompt = _oldPrompt;
            _oldPrompt = null;
        }
        WeakReferenceMessenger.Default.Send(new SnapshotMessage(!IsPromptEditorOpen));
        IsPromptEditorOpen = !IsPromptEditorOpen;
    }

    [RelayCommand]
    private async Task SavePrompt()
    {
        var selectedArchetype = SettingsVM.Instance.SelectedArchetype;
        var promptPath = Path.Combine(SettingsVM.PromptsPath, $"{selectedArchetype.Name}.txt");
        await File.WriteAllTextAsync(promptPath, selectedArchetype.Prompt);
        IsPromptEditorOpen = false;
    }
    
    [RelayCommand]
    private async Task ToggleFavoriteFact(object? args)
    {
        if (args is not KnowledgeRecord record)
            return;
        await DatabaseService.KnowledgeDb.UpdateFavoriteAsync(record.Id, record.IsFavorite);
    }
    
    [ObservableProperty] private bool _isMaximized;
}