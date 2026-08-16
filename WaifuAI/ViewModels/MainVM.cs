using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using WaifuAI.Models;
using WaifuAI.Services;
using CommunityToolkit.Mvvm.Messaging;
using SharpToken;
using WaifuAI.CustomClasses;
using Strings = WaifuAI.Translations.Strings;

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
        InitializingMessage = Strings.loading.settings_load.CurrentValue;
        await SettingsVM.Instance.Load();

        InitializingMessage = Strings.loading.start_web_server.CurrentValue;
        WebAddress = await Model3DService.StartWebServer();
        await Model3DService.WaitForResponce();

        await SettingsVM.Instance.InitializeModel3D();

        InitializingMessage = Strings.loading.start_voice_server.CurrentValue;
        VoiceService.StartPythonServer();
        await VoiceService.WaitForPythonServerAsync();

        await SettingsVM.Instance.InitializeSpeakers();

        InitializingMessage = Strings.loading.create_vector_generator.CurrentValue;
        await MessageParser.CreateVectorGenerator();

        InitializingMessage = Strings.loading.get_encoding.CurrentValue;
        await Task.Run(() => _encoder = GptEncoding.GetEncoding("cl100k_base"));

        InitializingMessage = Strings.loading.db_load.CurrentValue;
        await DatabaseService.InitializeDatabases();
        var records = await DatabaseService.KnowledgeDb.Table<KnowledgeRecord>().ToListAsync();
        KnowledgeBase.AddRange(records);

        InitializingMessage = Strings.loading.chat_load.CurrentValue;
        var messages = await DatabaseService.HistoryDb.Table<Message>()
            .OrderBy(m => m.Time)
            .ToListAsync();

        var messageVMs = messages.Select(msg => new MessageVM(msg, isSavedInDb: true)).ToList();
        var messageMap = messageVMs.ToDictionary(m => m.MessageModel.Id);

        foreach (var msg in messageVMs)
        {
            if (msg.MessageModel.ReplyMessageId == null ||
                !messageMap.TryGetValue(msg.MessageModel.ReplyMessageId.Value, out var replyMsg))
                continue;
            
            msg.ReplyMessage = replyMsg;
            replyMsg.ReplyingMessages.Add(msg);
        }

        _history.AddRange(messages);

        // скипнуть последний и добавить его отдельно чтобы к нему проскроллилось
        if (messageVMs.Count > 0)
        {
            Chat.AddRange(messageVMs.SkipLast(1));
            Chat.Add(messageVMs.Last());
        }

        SettingsVM.Instance.IsAppInitializing = false;

        KnowledgeBase.CollectionChanged += OnKnowledgeBaseChanged;
        SelectedMessages.CollectionChanged += OnSelectedMessagesChanged;

        DropService.OnWakeUpFirstMessageRequested += HandleWakeUpFirstMessageAsync;
        DropService.Start();

        if (SettingsVM.Instance.IsSleeping)
            StartSleepTimer();
        else
            StopSleepTimer();
        SettingsVM.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(SettingsVM.Instance.IsSleeping))
                return;
            
            if (SettingsVM.Instance.IsSleeping)
                StartSleepTimer();
            else
                StopSleepTimer();
        };

        SettingsVM.Instance.LastUserEntry = DateTime.UtcNow;

        SelectedMessages.CollectionChanged += (_, _) => 
            OnPropertyChanged(nameof(DeletingDialogMessage));
    }

    private static GptEncoding _encoder;

    public bool IsWindows => OperatingSystem.IsWindows();

    [ObservableProperty] private string _initializingMessage;
    [ObservableProperty] private string _webAddress;

    [ObservableProperty] 
    [NotifyCanExecuteChangedFor(nameof(RequestCommand))]
    private string _question = string.Empty;

    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(TokensText))]
    private int? _tokens;

    public string TokensText => string.Format(Strings.sidepanel.chat_panel.tokens.CurrentValue, Tokens);

    [ObservableProperty] private MessageVM? _selectedMessage;

    public ObservableCollection<MessageVM> SelectedMessages { get; } = [];

    [ObservableProperty] 
    [NotifyCanExecuteChangedFor(nameof(MultiSelectAddOrRemoveSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddOrRemoveSelectedCommand))]
    private bool _isMultiSelect;

    [ObservableProperty] private string? _error;

    [ObservableProperty] private MessageVM? _replyMessage;    
    // у сообщения которое готовится
    [ObservableProperty] private string? _quote;
    [ObservableProperty] private int _quoteStart;
    [ObservableProperty] private int _quoteEnd;

    [ObservableProperty] private bool _isSettingsOpen;
    [ObservableProperty] private bool _isPromptEditorOpen;
    [ObservableProperty] private bool _isDeletingMessageDialogOpen;
    [ObservableProperty] private bool _isDeletingRecordDialogOpen;

    public BulkObservableCollection<MessageVM> Chat { get; } = [];
    public BulkObservableCollection<KnowledgeRecord> KnowledgeBase { get; } = [];

    [ObservableProperty] private KnowledgeRecord? _selectedKnowledgeRecord;

    private CancellationTokenSource? _cts;
    [ObservableProperty] private bool _isGeneratingResponse;

    private DispatcherTimer? _sleepTimer;

    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(RemainingSleepTimeMask))]
    private string _remainingSleepTimeText = string.Empty;

    public string RemainingSleepTimeMask =>
        RemainingSleepTimeText.Length >= 8 ? "00:00:00" : "00:00";    

    public string DeletingDialogMessage => 
        string.Format(Strings.dialogs.deleting_message.CurrentValue, SelectedMessages.Count);

    public double ReplyButtonHeight => 48;


    private void StartSleepTimer()
    {
        _sleepTimer?.Stop();

        // Обновляем UI сразу, не дожидаясь первого тика
        UpdateRemainingTime();

        _sleepTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _sleepTimer.Tick += (_, _) => UpdateRemainingTime();
        _sleepTimer.Start();
    }

    private void StopSleepTimer()
    {
        _sleepTimer?.Stop();
        _sleepTimer = null;
        RemainingSleepTimeText = string.Empty;
    }

    private void UpdateRemainingTime()
    {
        var remaining = SettingsVM.Instance.WakeUpTime - DateTime.UtcNow;

        if (remaining <= TimeSpan.Zero)
        {
            RemainingSleepTimeText = "00:00:00";
            StopSleepTimer();
            SettingsVM.Instance.NotifySleepStatus();
            RequestCommand.NotifyCanExecuteChanged();
            return;
        }

        RemainingSleepTimeText = remaining.Hours > 0
            ? remaining.ToString(@"hh\:mm\:ss")
            : remaining.ToString(@"mm\:ss");
    }

    private async Task HandleWakeUpFirstMessageAsync()
    {
        // Если юзер прямо сейчас сам отправляет запрос — не мешаем ему
        if (IsGeneratingResponse)
            return;

        _cts?.CancelAsync();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsGeneratingResponse = true;
        var timestamp = DateTime.UtcNow;

        try
        {
            var query = new RequestModel
            {
                Temperature = SettingsVM.Instance.Temperature,
                MaxTokens = SettingsVM.Instance.MaxTokens
            };

            var lastMsgTime = _history.LastOrDefault()?.Time ?? timestamp;
            var factors = new Factors
            {
                DaysKnown = GetDaysKnown(),
                TimeSinceLastMessage = timestamp - lastMsgTime,
                RandomDailyNoise = SettingsVM.Instance.RandomDailyNoise
            };

            // 1. Формируем специальный системный промпт пробуждения
            var wakeUpPrompt = await PromptService.GetFullSystemPrompt(
                _baseSystemPrompt,
                _history,
                KnowledgeBase,
                question: string.Empty,
                factors,
                isInitiative: true);
            
            // 2. Берем последние 10 сообщений из истории
            var recentHistory = _history.TakeLast(10).ToList();

            // 3. Подрезаем по контексту и формируем итоговый пак сообщений
            var cuttedHistory = GetCuttedHistory(wakeUpPrompt, recentHistory, SettingsVM.Instance.ContextLength);
            cuttedHistory.Add(new Message
            {
                Role = "user",
                Content = "[System Event: You opened the app for the first time after waking up and decided to message the user first. The user hasn't come online yet. Initiate the conversation according to your system directives.]"
            });
            query.Messages.AddRange(cuttedHistory);

            // 4. Делаем запрос
            var messageModel = SettingsVM.Instance.IsServerQuery
                ? await RequestService.DoServerQuery(query, token)
                : await RequestService.DoProviderQuery(query, token);

            var resultMessage = new MessageVM(messageModel);

            // Если пришла системная ошибка — игнорируем инициативу
            if (resultMessage.MessageModel.Role == "system")
                return;

            // 5. Озвучиваем, добавляем в UI/БД и парсим эмоции/команды
            await ProcessReceivedMessage(resultMessage);
            await ParseReceivedMessage(resultMessage.MessageModel.Content, timestamp);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при отправке первого сообщения пробуждения: {ex.Message}");
        }
        finally
        {
            if (_cts?.Token == token)
                IsGeneratingResponse = false;
        }
    }

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
        if (!IsMultiSelect || SelectedMessages.Count != 0)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            IsMultiSelect = false;

            if (e.OldItems is null)
                return;

            foreach (var item in e.OldItems)
                if (item is MessageVM msg)
                    msg.IsSelected = false;
        });
    }

    private readonly List<Message> _history = [];

    private readonly Message _baseSystemPrompt = new()
    {
        Role = "system",
        Content = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prompt.txt"))
    };

    private bool CanDoRequest => 
        !string.IsNullOrWhiteSpace(Question) && 
        !SettingsVM.Instance.IsSleeping;

    [RelayCommand(CanExecute = nameof(CanDoRequest))]
    private async Task Request()
    {
        // ПЕРЕБИВАЕМ ТЕКУЩИЙ ЗАПРОС (если он идет)
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsGeneratingResponse = true;
        var timestamp = DateTime.UtcNow;

        // на случай вылета из приложения делаем это почаще
        SettingsVM.Instance.LastUserEntry = timestamp;

        try
        {
            CloseErrorMessage();

            var query = new RequestModel
            {
                Temperature = SettingsVM.Instance.Temperature,
                MaxTokens = SettingsVM.Instance.MaxTokens
            };

            var message = GetNewMessage(timestamp);
            _history.Add(message.MessageModel);

            var lastMsgTime = _history.LastOrDefault()?.Time ?? timestamp;
            var factors = new Factors
            {
                DaysKnown = GetDaysKnown(),
                TimeSinceLastMessage = timestamp - lastMsgTime,
                RandomDailyNoise = SettingsVM.Instance.RandomDailyNoise
            };
                
            var systemPrompt = await PromptService.GetFullSystemPrompt(
                _baseSystemPrompt,
                _history,
                KnowledgeBase,
                Question,
                factors);

            // определяем сколько истории добавить
            var cuttedHistory = GetCuttedHistory(
                systemPrompt,
                _history,
                SettingsVM.Instance.ContextLength);
            query.Messages.AddRange(cuttedHistory);

            Chat.Add(message);

            ReplyMessage?.ReplyingMessages.Add(message);
            Quote = null;
            QuoteStart = 0;
            QuoteEnd = 0;
            ReplyMessage = null;
            Question = string.Empty;

            // заглушка-загрузка
            var tempMessage = new MessageVM(
                messageModel: new Message { Role = "temp" }
            );
            Chat.Add(tempMessage);

            var messageModel = SettingsVM.Instance.IsServerQuery
                ? await RequestService.DoServerQuery(query, token)
                : await RequestService.DoProviderQuery(query, token);
            
            var resultMessage = new MessageVM(messageModel);
            Chat.Remove(tempMessage);

            if (resultMessage.MessageModel.Role == "system")
            {
                Question = message.MessageModel.CleanText;
                Error = resultMessage.MessageModel.Content;
                message.IsFailed = true;
                ReplyMessage = message.ReplyMessage;
                _history.Remove(_history.Last());
                if (ReplyMessage is null)
                    return;    
                Quote = message.Quote;
                QuoteStart = message.QuoteStart;
                QuoteEnd = message.QuoteEnd;
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            message.MessageModel.DesignHeight =
                WeakReferenceMessenger.Default.Send(new RequestMessageHeight<double>(Chat.IndexOf(message)));
            
            await DatabaseService.HistoryDb.InsertOrReplaceAsync(message.MessageModel);
            message.IsSavedInDb = true;

            await ProcessReceivedMessage(resultMessage);
            await ParseReceivedMessage(resultMessage.MessageModel.Content, timestamp);
        }
        catch (Exception e)
        {
            Console.WriteLine("Request error: " + e.Message);
        }
        finally
        {
            if (_cts?.Token == token)
                IsGeneratingResponse = false;
        }
    }

    private MessageVM GetNewMessage(DateTime timestamp)
    {
        var message = new MessageVM(messageModel: new()
        {
            Role = "user",
            CleanText = Question,
            Time = timestamp,
            Tokens = Tokens ?? 0
        });

        if (ReplyMessage?.IsReplied == true)
            message.MessageModel.Content +=
                $"[Replying to the {ReplyMessage.MessageModel.Role}'s message " +
                $"sent on {ReplyMessage.MessageModel.Time.ToLocalTime()} " +
                $"({PromptService.TimeAgoText(timestamp, ReplyMessage.MessageModel.Time)}): '{Quote}']\n\n" +
                $"{Question}";
        else if (ReplyMessage?.IsReplied == false)
            message.MessageModel.Content +=
                $"[Replying to the {ReplyMessage.MessageModel.Role}'s quote " +
                $"in message sent on {ReplyMessage.MessageModel.Time.ToLocalTime()} " +
                $"({PromptService.TimeAgoText(timestamp, ReplyMessage.MessageModel.Time)}): '{Quote}']\n\n" +
                $"{Question}";
        else
            message.MessageModel.Content += $"\n{Question}";
        
        message.ReplyMessage = ReplyMessage;
        message.Quote = Quote;
        message.QuoteStart = QuoteStart;
        message.QuoteEnd = QuoteEnd;

        return message;
    }

    private async Task ProcessReceivedMessage(MessageVM msg)
    {
        var messageText = msg.MessageModel.Content;
        VoiceService.Say(
            messageText,
            SettingsVM.Instance.SelectedSource,
            SettingsVM.Instance.SelectedVoiceModel,
            SettingsVM.Instance.SelectedLanguage,
            SettingsVM.Instance.SelectedSpeaker,
            SettingsVM.Instance.Volume,
            SettingsVM.Instance.Pitch,
            SettingsVM.Instance.Bass,
            SettingsVM.Instance.Treble,
            SettingsVM.Instance.IsStream);

        msg.MessageModel.CleanText = MessageParser.GetCleanText(messageText);

        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        msg.MessageModel.DesignHeight = 
            WeakReferenceMessenger.Default.Send(new RequestMessageHeight<double>(Chat.IndexOf(msg)));

        _history.Add(msg.MessageModel);

        await DatabaseService.HistoryDb.InsertOrReplaceAsync(msg.MessageModel);
        msg.IsSavedInDb = true;

        Chat.Add(msg);
    }

    private async Task ParseReceivedMessage(string text, DateTime timestamp)
    {
        var s = SettingsVM.Instance;

        await MessageParser.ParseTextForKnowledgeUpdates(text, KnowledgeBase);

        var isDating = MessageParser.ParseTextForDatingChange(text);
        if (isDating == false) // расставание
        {
            s.Affection = s.SelectedArchetype.BreakUpAffection;
            s.Mood = s.SelectedArchetype.BreakUpMood;
        }
        if (isDating != null)
            s.IsDating = (bool)isDating;

        var newName = MessageParser.ParseTextForLearnedName(text);
        if (newName != null)
            s.UserName = newName;

        var sleepTime = MessageParser.ParseWakeUpTime(text);
        if (sleepTime != null)
        {
            s.WakeUpTime = timestamp.AddMinutes((int)(sleepTime * 60));
        }

        UpdateEmotionalStates(text);
    }

    private List<Message> GetCuttedHistory(Message baseSystemPrompt, List<Message> history, int contextLength)
    {
        List<Message> result = [baseSystemPrompt];
        var totalContext = _encoder
            .Encode(baseSystemPrompt.Content)
            .Count;

        if (totalContext > contextLength)
            return [];

        List<Message> collectedMessages = [];

        for (int i = history.Count - 1; i >= 0; i--)
        {
            var tokens = _encoder.Encode(history[i].Content);
            totalContext += tokens.Count;

            if (totalContext <= contextLength)
                collectedMessages.Add(history[i]);
            else
                break;
        }

        collectedMessages.Reverse();
        result.AddRange(collectedMessages);

        return result;
    }

    private int GetDaysKnown()
    {
        if (_history.Count == 0)
            return 0;

        return _history
            .Select(m => m.Time.Date)
            .Distinct()
            .Count();
    }

    private void UpdateEmotionalStates(string text)
    {
        var deltas = MessageParser.ExtractDeltas(text);
        if (deltas is null)
            return;

        var affection = SettingsVM.Instance.Affection + deltas.AffectionDelta;
        var engagement = SettingsVM.Instance.Engagement + deltas.EngagementDelta;
        var mood = SettingsVM.Instance.Mood + deltas.MoodDelta;
        var energy = SettingsVM.Instance.Energy + deltas.EnergyDelta;

        SettingsVM.Instance.Affection = Math.Clamp(affection, 0, 100);
        SettingsVM.Instance.Engagement = Math.Clamp(engagement, 0, 100);
        SettingsVM.Instance.Mood = Math.Clamp(mood, 0, 100);
        SettingsVM.Instance.Energy = Math.Clamp(energy, 0, 100);
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
    private void ToggleMultiSelect(MessageVM sourceMsg)
    {
        if (IsMultiSelect)
        {
            foreach (var msg in SelectedMessages)
            {
                msg.IsSelected = false;
            }

            SelectedMessages.Clear();
            IsMultiSelect = false;
        }
        else
        {
            foreach (var msg in SelectedMessages)
            {
                msg.IsSelected = false;
            }

            SelectedMessages.Clear();

            sourceMsg.IsSelected = true;
            SelectedMessages.Add(sourceMsg);
            SelectedMessage = sourceMsg;

            IsMultiSelect = true;
        }
    }

    [RelayCommand]
    private void DeletingMessageDialog()
    {
        WeakReferenceMessenger.Default.Send(
            new SnapshotMessage(!IsDeletingMessageDialogOpen)
        );
        IsDeletingMessageDialogOpen = !IsDeletingMessageDialogOpen;
    }
    
    [RelayCommand]
    private async Task DeleteMessage()
    {
        var messagesToDelete = SelectedMessages.ToList();

        List<MessageVM> messagesWithNewHeight = [];

        foreach (var msg in messagesToDelete)
        {
            _history.Remove(msg.MessageModel);

            foreach (var replyingMsg in msg.ReplyingMessages)
            {
                replyingMsg.MessageModel.Content = replyingMsg.MessageModel.CleanText;
                
                messagesWithNewHeight.Add(replyingMsg);

                replyingMsg.ReplyMessage = null;
            }
            msg.ReplyMessage?.ReplyingMessages.Remove(msg);

            await DatabaseService.HistoryDb.DeleteAsync(msg.MessageModel);
            Chat.Remove(msg);

            if (msg.MessageModel.Id == ReplyMessage?.MessageModel.Id)
                ReleaseReplyAndQuote();
        }

        foreach (var msg in messagesWithNewHeight)
        {
            msg.MessageModel.DesignHeight -= ReplyButtonHeight;
            await DatabaseService.HistoryDb.UpdateAsync(msg.MessageModel);
        }

        SelectedMessages.Clear();
        SelectedMessage = null;
        IsMultiSelect = false;
        IsDeletingMessageDialogOpen = false;
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

    [RelayCommand]
    private void DeletingRecordDialog()
    {
        WeakReferenceMessenger.Default.Send(new SnapshotMessage(!IsDeletingRecordDialogOpen));
        IsDeletingRecordDialogOpen = !IsDeletingRecordDialogOpen;
    }

    [RelayCommand]
    private async Task DeleteKnowledgeRecord()
    {
        if (SelectedKnowledgeRecord is null)
            return;
        
        KnowledgeBase.Remove(SelectedKnowledgeRecord);
        IsDeletingRecordDialogOpen = false;
    }
    
    [ObservableProperty] private bool _isMaximized;

    [RelayCommand]
    private void InsertNewLine(object? parameter)
    {
        if (parameter is not TextBox textBox)
            return;
        
        int caretIndex = textBox.CaretIndex;
        string text = textBox.Text ?? "";

        textBox.Text = text.Insert(caretIndex, Environment.NewLine);
        textBox.CaretIndex = caretIndex + Environment.NewLine.Length;
    }

    public bool CanAddOrRemoveSelected => !IsMultiSelect;

    [RelayCommand(CanExecute = nameof(CanAddOrRemoveSelected))]
    private void AddOrRemoveSelected(MessageVM msg)
    {
        if (SelectedMessage != msg)
        {
            SelectedMessage?.IsSelected = false;
            if (SelectedMessage != null) 
                SelectedMessages.Remove(SelectedMessage);
        }
        msg.IsSelected = true;
        if (!SelectedMessages.Contains(msg))
            SelectedMessages.Add(msg);
        SelectedMessage = msg;
    }

    public bool CanMultiSelectAddOrRemoveSelected => IsMultiSelect;

    [RelayCommand(CanExecute = nameof(CanMultiSelectAddOrRemoveSelected))]
    private void MultiSelectAddOrRemoveSelected(MessageVM msg)
    {
        if (SelectedMessages.Contains(msg))
        {
            SelectedMessages.Remove(msg);
            msg.IsSelected = false;
        }
        else
        {
            SelectedMessages.Add(msg);
            msg.IsSelected = true;
        }
    }

    [RelayCommand]
    private async Task RequestTest()
    {
        var settings = SettingsVM.Instance;

        var text = settings.SelectedLanguage switch
        {
            "ru" =>
                "Привет! Я рада тебя видеть. Сегодня у меня отличное настроение, так что давай немного поболтаем и проверим, как звучит мой голос.",
            "fr" =>
                "Bonjour ! Je suis contente de te voir. Aujourd’hui, je suis de très bonne humeur, alors discutons un peu et voyons comment ma voix sonne.",
            "es" =>
                "¡Hola! Me alegra mucho verte. Hoy estoy de muy buen humor, así que vamos a charlar un poco y comprobar cómo suena mi voz.",
            "de" =>
                "Hallo! Ich freue mich, dich zu sehen. Heute bin ich besonders gut gelaunt, also lass uns ein bisschen plaudern und meine Stimme testen.",
            _ =>
                "Hello! I’m happy to see you. I’m in a really good mood today, so let’s have a little chat and see how my voice sounds."
        };

        WeakReferenceMessenger.Default.Send(
            new ExecuteScriptMessage("window.vrmApp.isTestRequested = true")
        );

        var isTestRequested = false;
        while (!isTestRequested)
        {
            var msg = new EvaluateScriptMessage<bool>("return window.vrmApp.isTestRequested");
            isTestRequested = await WeakReferenceMessenger.Default.Send(msg);
            await Task.Delay(100);
        }

        VoiceService.Say(
            text,
            settings.SelectedSource,
            settings.SelectedVoiceModel,
            settings.SelectedLanguage,
            settings.SelectedSpeaker,
            settings.Volume,
            settings.Pitch,
            settings.Bass,
            settings.Treble,
            isStream: false);

        while (isTestRequested)
        {
            var msg = new EvaluateScriptMessage<bool>("return window.vrmApp.isTestRequested");
            isTestRequested = await WeakReferenceMessenger.Default.Send(msg);
            await Task.Delay(100);
        }
    }
}