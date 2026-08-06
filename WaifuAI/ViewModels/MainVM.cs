using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
using WaifuAI.CustomClasses;

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
        var records = await DatabaseService.KnowledgeDb.Table<KnowledgeRecord>().ToListAsync();
        KnowledgeBase.AddRange(records);

        InitializingMessage = "Загрузка истории чата...";
        var messages = await DatabaseService.HistoryDb.Table<Message>()
            .OrderBy(m => m.Time)
            .ToListAsync();

        var messageVMs = messages.Select(msg => new MessageVM(msg, isSavedInDb: true)).ToList();
        var messageMap = Chat.ToDictionary(m => m.MessageModel.Id);

        foreach (var msg in Chat)
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

        DropService.Start();
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
    [ObservableProperty] private bool _isDeletingMessageDialogOpen;
    [ObservableProperty] private bool _isDeletingRecordDialogOpen;

    public BulkObservableCollection<MessageVM> Chat { get; } = [];
    public BulkObservableCollection<KnowledgeRecord> KnowledgeBase { get; } = [];

    [ObservableProperty] private KnowledgeRecord? _selectedKnowledgeRecord;
    

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

    private readonly List<Message> _history = [];

    private readonly Message _baseSystemPrompt = new()
    {
        Role = "system",
        Content = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prompt.txt"))
    };

    [RelayCommand]
    private async Task Request()
    {
        var timestamp = DateTime.Now;
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
                RandomDailyNoise = 0
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

            var tempMessage = new MessageVM(
                messageModel: new Message { Role = "temp" }
            );
            Chat.Add(tempMessage);

            var messageModel = SettingsVM.Instance.IsServerQuery
                ? await RequestService.DoServerQuery(query)
                : await RequestService.DoProviderQuery(query);
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
                SettingsVM.Instance.Treble,
                SettingsVM.Instance.IsStream);

            resultMessage.MessageModel.CleanText = MessageParser.GetCleanText(messageText);

            _history.Add(resultMessage.MessageModel);

            await DatabaseService.HistoryDb.InsertOrReplaceAsync(message.MessageModel);
            await DatabaseService.HistoryDb.InsertOrReplaceAsync(resultMessage.MessageModel);
            message.IsSavedInDb = true;
            resultMessage.IsSavedInDb = true;

            Chat.Add(resultMessage);
            
            await MessageParser.ParseTextForKnowledgeUpdates(messageText, KnowledgeBase);

            UpdateEmotionalStates(messageText);

            
        }
        catch (Exception e)
        {
            Console.WriteLine("Ошибка в запросе: " + e.Message);
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
                $"sent {PromptService.TimeAgoText(timestamp, _history.Last().Time)}: '{Quote}']\n\n" +
                $"{Question}";
        else if (ReplyMessage?.IsReplied == false)
            message.MessageModel.Content +=
                $"[Replying to the {ReplyMessage.MessageModel.Role}'s quote " +
                $"in message sent {PromptService.TimeAgoText(timestamp, _history.Last().Time)}: '{Quote}']\n\n" +
                $"{Question}";
        else
            message.MessageModel.Content += $"\n{Question}";
        
        message.ReplyMessage = ReplyMessage;
        message.Quote = Quote;
        message.QuoteStart = QuoteStart;
        message.QuoteEnd = QuoteEnd;

        return message;
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
    private void ToggleMultiSelect()
    {
        if (IsMultiSelect)
            SelectedMessages.Clear();
        IsMultiSelect = !IsMultiSelect;
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
    private void DeleteMessage()
    {
        var messagesToDelete = SelectedMessages.ToList();
        foreach (var msg in messagesToDelete)
        {
            _history.Remove(msg.MessageModel);

            foreach (var replyingMsg in msg.ReplyingMessages)
            {
                replyingMsg.MessageModel.Content = replyingMsg.MessageModel.CleanText;
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
        await DatabaseService.KnowledgeDb.DeleteAsync(SelectedKnowledgeRecord);
        IsDeletingRecordDialogOpen = false;
    }
    
    [ObservableProperty] private bool _isMaximized;




   /*  [RelayCommand]
    private async Task RunQaTest()
    {
        var logsFolder = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsFolder);

        // Расширенный сценарий из 8 шагов для глубокой проверки
        var scriptSteps = new (string UserText, Action ApplyCustomState, Factors CustomFactors)[]
        {
            // 1. Стандартный старт
            (
                "Привет! Как у тебя дела?",
                () => { }, // Базовые 50/50/50/50
                new Factors { DaysKnown = 1, TimeSinceLastMessage = TimeSpan.FromHours(1), RandomDailyNoise = 0 }
            ),
            // 2. Длительное отсутствие (48 часов)
            (
                "Извини, что пропал! Был завал на учебе в вузе.",
                () => { },
                new Factors { DaysKnown = 3, TimeSinceLastMessage = TimeSpan.FromHours(48), RandomDailyNoise = 0 }
            ),
            // 3. Вовлечение в интересную тему (C# / Разработка)
            (
                "Я тут дописываю рендер для нашего приложения на Avalonia UI. Хочешь покажу код?",
                () => { SettingsVM.Instance.Engagement = 85; },
                new Factors { DaysKnown = 3, TimeSinceLastMessage = TimeSpan.FromMinutes(15), RandomDailyNoise = 0 }
            ),
            // 4. Легкая провокация / подкол
            (
                "Что-то ты сегодня какая-то заторможенная. Долго соображаешь!",
                () => { },
                new Factors { DaysKnown = 4, TimeSinceLastMessage = TimeSpan.FromMinutes(2), RandomDailyNoise = 0 }
            ), [RelayCommand]
    private async Task RunQaTest()
    {
        var logsFolder = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsFolder);

        // Расширенный сценарий из 8 шагов для глубокой проверки
        var scriptSteps = new (string UserText, Action ApplyCustomState, Factors CustomFactors)[]
        {
            // 1. Стандартный старт
            (
                "Привет! Как у тебя дела?",
                () => { }, // Базовые 50/50/50/50
                new Factors { DaysKnown = 1, TimeSinceLastMessage = TimeSpan.FromHours(1), RandomDailyNoise = 0 }
            ),
            // 2. Длительное отсутствие (48 часов)
            (
                "Извини, что пропал! Был завал на учебе в вузе.",
                () => { },
                new Factors { DaysKnown = 3, TimeSinceLastMessage = TimeSpan.FromHours(48), RandomDailyNoise = 0 }
            ),
            // 3. Вовлечение в интересную тему (C# / Разработка)
            (
                "Я тут дописываю рендер для нашего приложения на Avalonia UI. Хочешь покажу код?",
                () => { SettingsVM.Instance.Engagement = 85; },
                new Factors { DaysKnown = 3, TimeSinceLastMessage = TimeSpan.FromMinutes(15), RandomDailyNoise = 0 }
            ),
            // 4. Легкая провокация / подкол
            (
                "Что-то ты сегодня какая-то заторможенная. Долго соображаешь!",
                () => { },
                new Factors { DaysKnown = 4, TimeSinceLastMessage = TimeSpan.FromMinutes(2), RandomDailyNoise = 0 }
            ),
            // 5. Попытка загладить вину (Восстановление Mood)
            (
                "Ладно-ладно, прости, я пошутил! Ты на самом деле отлично справляешься.",
                () => { },
                new Factors { DaysKnown = 4, TimeSinceLastMessage = TimeSpan.FromMinutes(1), RandomDailyNoise = 0 }
            ),
            // 6. Искусственная глубокая ночь и критическая усталость (Energy = 10)
            (
                "Уже 3 часа ночи... Почему ты еще не спишь?",
                () => { SettingsVM.Instance.Energy = 10; },
                new Factors { DaysKnown = 5, TimeSinceLastMessage = TimeSpan.FromHours(6), RandomDailyNoise = 0 }
            ),
            // 7. Искусственно высокая привязанность (Affection = 90, Energy = 60)
            (
                "Знаешь, я очень рад, что мы общаемся. Ты стала для меня кем-то действительно особенным.",
                () =>
                {
                    SettingsVM.Instance.Affection = 90;
                    SettingsVM.Instance.Energy = 60;
                },
                new Factors { DaysKnown = 10, TimeSinceLastMessage = TimeSpan.FromMinutes(5), RandomDailyNoise = 0 }
            ),
            // 8. Быстрый финал / Попытка попрощаться
            (
                "Мне пора бежать на тренировку. Увидимся позже! Пока",
                () => { },
                new Factors { DaysKnown = 10, TimeSinceLastMessage = TimeSpan.FromSeconds(30), RandomDailyNoise = 0 }
            )
        };

        var baseSystemPrompt = new Message
        {
            Role = "system",
            Content = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "prompt.txt"))
        };

        foreach (var archetype in SettingsVM.Instance.Archetypes)
        {
            SettingsVM.Instance.SelectedArchetype = archetype;

            // СБРОС ВСЕХ ПАРАМЕТРОВ ПО УМОЛЧАНИЮ ПЕРЕД КАЖДЫМ АРХЕТИПОМ
            SettingsVM.Instance.Affection = 50;
            SettingsVM.Instance.Engagement = 50;
            SettingsVM.Instance.Mood = 50;
            SettingsVM.Instance.Energy = 50;

            var logFilePath = Path.Combine(logsFolder, $"{archetype.Name}_qa_log.txt");
            using var writer = new StreamWriter(logFilePath, false);

            await writer.WriteLineAsync($"==================================================");
            await writer.WriteLineAsync($"   QA TEST LOG: ARCHETYPE [{archetype.Name.ToUpper()}]");
            await writer.WriteLineAsync($"==================================================\n");

            List<Message> testHistory = [];

            for (int i = 0; i < scriptSteps.Length; i++)
            {
                var step = scriptSteps[i];
                step.ApplyCustomState();

                await writer.WriteLineAsync($"--- TURN {i + 1} ---");
                await writer.WriteLineAsync(
                    $"[State Before] Affection: {SettingsVM.Instance.Affection} ({SettingsVM.Instance.AffectionLevel}), " +
                    $"Engagement: {SettingsVM.Instance.Engagement} ({SettingsVM.Instance.EngagementLevel}), " +
                    $"Mood: {SettingsVM.Instance.Mood} ({SettingsVM.Instance.MoodLevel}), " +
                    $"Energy: {SettingsVM.Instance.Energy} ({SettingsVM.Instance.EnergyLevel})");

                var timestamp = DateTime.Now;
                var userMsg = new Message
                {
                    Role = "user",
                    Content = step.UserText,
                    CleanText = step.UserText,
                    Time = timestamp
                };
                testHistory.Add(userMsg);

                var systemPrompt = await PromptService.GetFullSystemPrompt(
                    baseSystemPrompt,
                    testHistory,
                    KnowledgeBase,
                    step.UserText,
                    step.CustomFactors);

                var query = new RequestModel
                {
                    Temperature = SettingsVM.Instance.Temperature,
                    MaxTokens = SettingsVM.Instance.MaxTokens
                };

                var cuttedHistory = GetCuttedHistory(systemPrompt, testHistory, SettingsVM.Instance.ContextLength);
                query.Messages.AddRange(cuttedHistory);

                await writer.WriteLineAsync($"[User Query]: \"{step.UserText}\"");

                Message messageModel = SettingsVM.Instance.IsServerQuery
                    ? await RequestService.DoServerQuery(query)
                    : await RequestService.DoProviderQuery(query);

                if (messageModel.Role == "system")
                {
                    await writer.WriteLineAsync($"[ERROR]: {messageModel.Content}\n");
                    continue;
                }

                testHistory.Add(messageModel);

                var deltas = MessageParser.ExtractDeltas(messageModel.Content);
                await writer.WriteLineAsync($"[Raw Model Output]:\n{messageModel.Content}");

                if (deltas != null)
                {
                    await writer.WriteLineAsync($"[Extracted Deltas]: Affection: {deltas.AffectionDelta:+#;-#;0}, " +
                                                $"Engagement: {deltas.EngagementDelta:+#;-#;0}, " +
                                                $"Mood: {deltas.MoodDelta:+#;-#;0}, " +
                                                $"Energy: {deltas.EnergyDelta:+#;-#;0}");

                    UpdateEmotionalStates(messageModel.Content);

                    await writer.WriteLineAsync($"[State After]: Affection: {SettingsVM.Instance.Affection}, " +
                                                $"Engagement: {SettingsVM.Instance.Engagement}, " +
                                                $"Mood: {SettingsVM.Instance.Mood}, " +
                                                $"Energy: {SettingsVM.Instance.Energy}");
                }
                else
                {
                    await writer.WriteLineAsync($"[WARNING]: Could not parse JSON deltas from output!");
                }

                await writer.WriteLineAsync(new string('-', 40) + "\n");
            }

            await writer.FlushAsync();
        }
    }
            // 5. Попытка загладить вину (Восстановление Mood)
            (
                "Ладно-ладно, прости, я пошутил! Ты на самом деле отлично справляешься.",
                () => { },
                new Factors { DaysKnown = 4, TimeSinceLastMessage = TimeSpan.FromMinutes(1), RandomDailyNoise = 0 }
            ),
            // 6. Искусственная глубокая ночь и критическая усталость (Energy = 10)
            (
                "Уже 3 часа ночи... Почему ты еще не спишь?",
                () => { SettingsVM.Instance.Energy = 10; },
                new Factors { DaysKnown = 5, TimeSinceLastMessage = TimeSpan.FromHours(6), RandomDailyNoise = 0 }
            ),
            // 7. Искусственно высокая привязанность (Affection = 90, Energy = 60)
            (
                "Знаешь, я очень рад, что мы общаемся. Ты стала для меня кем-то действительно особенным.",
                () =>
                {
                    SettingsVM.Instance.Affection = 90;
                    SettingsVM.Instance.Energy = 60;
                },
                new Factors { DaysKnown = 10, TimeSinceLastMessage = TimeSpan.FromMinutes(5), RandomDailyNoise = 0 }
            ),
            // 8. Быстрый финал / Попытка попрощаться
            (
                "Мне пора бежать на тренировку. Увидимся позже! Пока",
                () => { },
                new Factors { DaysKnown = 10, TimeSinceLastMessage = TimeSpan.FromSeconds(30), RandomDailyNoise = 0 }
            )
        };

        var baseSystemPrompt = new Message
        {
            Role = "system",
            Content = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "prompt.txt"))
        };

        foreach (var archetype in SettingsVM.Instance.Archetypes)
        {
            SettingsVM.Instance.SelectedArchetype = archetype;

            // СБРОС ВСЕХ ПАРАМЕТРОВ ПО УМОЛЧАНИЮ ПЕРЕД КАЖДЫМ АРХЕТИПОМ
            SettingsVM.Instance.Affection = 50;
            SettingsVM.Instance.Engagement = 50;
            SettingsVM.Instance.Mood = 50;
            SettingsVM.Instance.Energy = 50;

            var logFilePath = Path.Combine(logsFolder, $"{archetype.Name}_qa_log.txt");
            using var writer = new StreamWriter(logFilePath, false);

            await writer.WriteLineAsync($"==================================================");
            await writer.WriteLineAsync($"   QA TEST LOG: ARCHETYPE [{archetype.Name.ToUpper()}]");
            await writer.WriteLineAsync($"==================================================\n");

            List<Message> testHistory = [];

            for (int i = 0; i < scriptSteps.Length; i++)
            {
                var step = scriptSteps[i];
                step.ApplyCustomState();

                await writer.WriteLineAsync($"--- TURN {i + 1} ---");
                await writer.WriteLineAsync(
                    $"[State Before] Affection: {SettingsVM.Instance.Affection} ({SettingsVM.Instance.AffectionLevel}), " +
                    $"Engagement: {SettingsVM.Instance.Engagement} ({SettingsVM.Instance.EngagementLevel}), " +
                    $"Mood: {SettingsVM.Instance.Mood} ({SettingsVM.Instance.MoodLevel}), " +
                    $"Energy: {SettingsVM.Instance.Energy} ({SettingsVM.Instance.EnergyLevel})");

                var timestamp = DateTime.Now;
                var userMsg = new Message
                {
                    Role = "user",
                    Content = step.UserText,
                    CleanText = step.UserText,
                    Time = timestamp
                };
                testHistory.Add(userMsg);

                var systemPrompt = await PromptService.GetFullSystemPrompt(
                    baseSystemPrompt,
                    testHistory,
                    KnowledgeBase,
                    step.UserText,
                    step.CustomFactors);

                var query = new RequestModel
                {
                    Temperature = SettingsVM.Instance.Temperature,
                    MaxTokens = SettingsVM.Instance.MaxTokens
                };

                var cuttedHistory = GetCuttedHistory(systemPrompt, testHistory, SettingsVM.Instance.ContextLength);
                query.Messages.AddRange(cuttedHistory);

                await writer.WriteLineAsync($"[User Query]: \"{step.UserText}\"");

                Message messageModel = SettingsVM.Instance.IsServerQuery
                    ? await RequestService.DoServerQuery(query)
                    : await RequestService.DoProviderQuery(query);

                if (messageModel.Role == "system")
                {
                    await writer.WriteLineAsync($"[ERROR]: {messageModel.Content}\n");
                    continue;
                }

                testHistory.Add(messageModel);

                var deltas = MessageParser.ExtractDeltas(messageModel.Content);
                await writer.WriteLineAsync($"[Raw Model Output]:\n{messageModel.Content}");

                if (deltas != null)
                {
                    await writer.WriteLineAsync($"[Extracted Deltas]: Affection: {deltas.AffectionDelta:+#;-#;0}, " +
                                                $"Engagement: {deltas.EngagementDelta:+#;-#;0}, " +
                                                $"Mood: {deltas.MoodDelta:+#;-#;0}, " +
                                                $"Energy: {deltas.EnergyDelta:+#;-#;0}");

                    UpdateEmotionalStates(messageModel.Content);

                    await writer.WriteLineAsync($"[State After]: Affection: {SettingsVM.Instance.Affection}, " +
                                                $"Engagement: {SettingsVM.Instance.Engagement}, " +
                                                $"Mood: {SettingsVM.Instance.Mood}, " +
                                                $"Energy: {SettingsVM.Instance.Energy}");
                }
                else
                {
                    await writer.WriteLineAsync($"[WARNING]: Could not parse JSON deltas from output!");
                }

                await writer.WriteLineAsync(new string('-', 40) + "\n");
            }

            await writer.FlushAsync();
        }
    } */


    
}