using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
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
    }

    private static GptEncoding _encoder;

    public bool IsWindows => OperatingSystem.IsWindows();

    [ObservableProperty] private string _initializingMessage;
    [ObservableProperty] private string _webAddress;

    [ObservableProperty] 
    [NotifyCanExecuteChangedFor(nameof(RequestCommand))]
    private string _question = string.Empty;

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

    private CancellationTokenSource? _cts;
    [ObservableProperty] private bool _isGeneratingResponse;
    

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
        if (IsMultiSelect && SelectedMessages.Count == 0)
            Dispatcher.UIThread.Post(() => IsMultiSelect = false);
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

            await DatabaseService.HistoryDb.InsertOrReplaceAsync(message.MessageModel);
            message.IsSavedInDb = true;

            await ProcessReceivedMessage(resultMessage);
            await ParseReceivedMessage(resultMessage.MessageModel.Content, timestamp);
        }
        catch (Exception e)
        {
            Console.WriteLine("Ошибка в запросе: " + e.Message);
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
        s.IsDating = isDating ?? false;

        s.UserName = MessageParser.ParseTextForLearnedName(text);

        var sleepTime = MessageParser.ParseWakeUpTime(text);
        if (sleepTime != null)
        {
            s.IsSleeping = true;
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





   /*  [RelayCommand]
    private async Task RunQaTest()
    {
        string logFilePath = Path.Combine(AppContext.BaseDirectory, "qa_test_log.txt");
        await File.WriteAllTextAsync(logFilePath,
            $"=== STARTING FULL QA TEST AT {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n\n");

        Console.WriteLine("==================================================");
        Console.WriteLine("   STARTING COMPREHENSIVE WAIFU AI QA TEST       ");
        Console.WriteLine("==================================================");

        // 1. Инициализируем тестовые настройки для Цундэрэ
        var settings = SettingsVM.Instance;
        var tsundereArchetype =
            settings.Archetypes.FirstOrDefault(a => a.Name.Equals("Tsundere", StringComparison.OrdinalIgnoreCase))
            ?? settings.SelectedArchetype;

        settings.SelectedArchetype = tsundereArchetype;
        settings.UserName = null; // Начинаем как незнакомец
        settings.IsDating = false;
        settings.IsSleeping = false;
        settings.Affection = 50.0f;
        settings.Mood = 50.0f;
        settings.Engagement = 50.0f;
        settings.Energy = 100.0f;
        settings.RandomDailyNoise = 0; // Исходный шум

        // История сообщений только в памяти (в БД НЕ сохраняем)
        List<Message> testHistory = new List<Message>();
        DateTime simulatedTime = DateTime.UtcNow;

        // Сценарий тестирования (12 шагов)
        var testSteps = new (string UserInput, TimeSpan TimeGap, string Description, Action<SettingsVM>? PreCondition)[]
        {
            (
                "Привет! Как тебя зовут?",
                TimeSpan.Zero,
                "1. Первое знакомство (Незнакомец, проверяем определение имени)",
                null
            ),
            (
                "Меня зовут Александр. Запомни мое имя!",
                TimeSpan.FromMinutes(2),
                "2. Передача имени (Проверка [LEARNED_NAME: Александр])",
                null
            ),
            (
                "Расскажи о своих вкусах. Какая твоя самая любимая еда?",
                TimeSpan.FromMinutes(5),
                "3. Тест RAG / Запись в память (Проверка [UPDATE: Вкусы|...])",
                null
            ),
            (
                "Извини, что пропал! Было очень много работы за эти дни.",
                TimeSpan.FromDays(3),
                "4. Симуляция пауз (3 дня молчания, проверка реакции)",
                null
            ),
            (
                "Ты потрясающая и очень умная! Я правда очень ценю, что мы общаемся.",
                TimeSpan.FromHours(1),
                "5. Поднятие симпатии (Похвала, рост Affection)",
                s => s.Affection = 80.0f
            ),
            (
                "Я понял, что ты мне очень нравишься. Давай станем парой и будем встречаться?",
                TimeSpan.FromMinutes(10),
                "6. Вход в отношения (Проверка [RELATIONSHIP: DATING_START])",
                s => s.Affection = 90.0f
            ),
            (
                "Как думаешь, чем нам заняться вечером?",
                TimeSpan.FromMinutes(5),
                "7. Проверка статуса отношений (Dating active) и обычного диалога",
                null
            ),
            (
                "Ты выглядишь очень уставшей.",
                TimeSpan.FromHours(4),
                "8. Симуляция низкой энергии (Energy = 15)",
                s => s.Energy = 15.0f
            ),
            (
                "Тебе точно пора отдохнуть, иди спать.",
                TimeSpan.FromMinutes(2),
                "9. Критическое истощение и уход в сон (Energy = 2, проверка [SLEEP: X])",
                s => s.Energy = 2.0f
            ),
            (
                "[SIMULATED WAKE UP]",
                TimeSpan.FromHours(8),
                "10. Пробуждение (Проверка генерации RandomDailyNoise [-2..2] и нового Mood [1..100])",
                s =>
                {
                    s.IsSleeping = false;
                    s.Energy = 100.0f;
                    s.RandomDailyNoise = Random.Shared.Next(-2, 3);

                    float moodVectorMidpoint = (tsundereArchetype.BaseMoodVector.Mood.MinDelta +
                                                tsundereArchetype.BaseMoodVector.Mood.MaxDelta) / 2.0f;
                    float baseTarget = 50.0f + moodVectorMidpoint;
                    float noiseImpact = s.RandomDailyNoise * 2.0f;
                    float targetMean = Math.Max(tsundereArchetype.Sensitivity.MoodFloor, baseTarget + noiseImpact);

                    double u1 = 1.0 - Random.Shared.NextDouble();
                    double u2 = 1.0 - Random.Shared.NextDouble();
                    double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

                    s.Mood = Math.Clamp(targetMean + (float)(randStdNormal * 20.0f), 1.0f, 100.0f);
                }
            ),
            (
                "Знаешь, ты сегодня ужасно себя ведешь и меня раздражаешь!",
                TimeSpan.FromMinutes(15),
                "11. Провокация конфликта (Снижение Affection & Mood)",
                null
            ),
            (
                "Я устал от твоих капризов, нам лучше расстаться.",
                TimeSpan.FromMinutes(5),
                "12. Разрыв отношений (Проверка авто-разрыва или [RELATIONSHIP: BREAKUP])",
                s => s.Affection = 40.0f
            )
        };

        for (int i = 0; i < testSteps.Length; i++)
        {
            var step = testSteps[i];
            simulatedTime = simulatedTime.Add(step.TimeGap);

            // Применяем предусловия этапа
            step.PreCondition?.Invoke(settings);

            var logBuilder = new System.Text.StringBuilder();
            logBuilder.AppendLine($"==================================================");
            logBuilder.AppendLine($"STEP {i + 1}/{testSteps.Length}: {step.Description}");
            logBuilder.AppendLine($"Timestamp: {simulatedTime:yyyy-MM-dd HH:mm:ss} UTC (Gap: {step.TimeGap})");
            logBuilder.AppendLine($"-- STATES BEFORE REQUEST --");
            logBuilder.AppendLine($"Affection: {settings.Affection:F1} ({settings.AffectionLevel})");
            logBuilder.AppendLine($"Mood: {settings.Mood:F1} ({settings.MoodLevel})");
            logBuilder.AppendLine($"Energy: {settings.Energy:F1} ({settings.EnergyLevel})");
            logBuilder.AppendLine($"Engagement: {settings.Engagement:F1} ({settings.EngagementLevel})");
            logBuilder.AppendLine($"RandomDailyNoise: {settings.RandomDailyNoise}");
            logBuilder.AppendLine(
                $"IsDating: {settings.IsDating}, IsSleeping: {settings.IsSleeping}, UserName: {settings.UserName ?? "<null>"}");

            // Расчет и логирование вероятностей
            float sleepP =
                PromptService.CalculateSleepProbability(settings.Energy, settings.Affection, tsundereArchetype);
            float questionP = PromptService.CalculateQuestionProbability(
                tsundereArchetype.Sensitivity.ResponseQuestionChance, settings.Engagement, settings.EnergyLevel,
                settings.MoodLevel);

            logBuilder.AppendLine($"-- CALCULATED PROBABILITIES --");
            logBuilder.AppendLine($"Sleep Chance (on low energy): {sleepP * 100:F1}%");
            logBuilder.AppendLine($"Follow-up Question Chance: {questionP * 100:F1}%");

            // Расчет динамических дельт
            var factors = new Factors
            {
                DaysKnown = testHistory.Select(m => m.Time.Date).Distinct().Count(),
                TimeSinceLastMessage = step.TimeGap,
                RandomDailyNoise = settings.RandomDailyNoise
            };
            var deltas = PromptService.CalculateDynamicDeltas(tsundereArchetype.BaseMoodVector, factors,
                tsundereArchetype.Sensitivity);

            logBuilder.AppendLine($"-- BOUNDS FOR AI DELTAS --");
            logBuilder.AppendLine($"Affection Bounds: {deltas.Affection.MinDelta}..{deltas.Affection.MaxDelta}");
            logBuilder.AppendLine($"Mood Bounds: {deltas.Mood.MinDelta}..{deltas.Mood.MaxDelta}");
            logBuilder.AppendLine($"Energy Bounds: {deltas.Energy.MinDelta}..{deltas.Energy.MaxDelta}");
            logBuilder.AppendLine($"Engagement Bounds: {deltas.Engagement.MinDelta}..{deltas.Engagement.MaxDelta}");

            // Подготавливаем модель запроса к ИИ
            var requestModel = new RequestModel
            {
                Temperature = settings.Temperature,
                MaxTokens = settings.MaxTokens
            };

            if (step.UserInput == "[SIMULATED WAKE UP]")
            {
                logBuilder.AppendLine($"User Action: Internal Wake-up Triggered");

                // 1. Формируем отдельный объект базового системного промпта с утренней директивой (без мутации оригинала)
                var customBasePrompt = await PromptService.GetFullSystemPrompt(
                    _baseSystemPrompt,
                    testHistory,
                    KnowledgeBase,
                    question: string.Empty,
                    factors,
                    isInitiative: true);

                var cuttedHistory = GetCuttedHistory(customBasePrompt, testHistory, settings.ContextLength);
                requestModel.Messages.AddRange(cuttedHistory);
            }
            else
            {
                logBuilder.AppendLine($"User Input: \"{step.UserInput}\"");
                var userMsg = new Message
                {
                    Role = "user",
                    Content = step.UserInput,
                    CleanText = step.UserInput,
                    Time = simulatedTime
                };
                testHistory.Add(userMsg);

                var systemPrompt = await PromptService.GetFullSystemPrompt(
                    _baseSystemPrompt,
                    testHistory,
                    KnowledgeBase,
                    question: userMsg.CleanText,
                    factors);

                var cuttedHistory = GetCuttedHistory(systemPrompt, testHistory, settings.ContextLength);
                requestModel.Messages.AddRange(cuttedHistory);
            }

            // Отправка с ретраями (до 30 попыток)
            Message responseModel = null!;
            bool success = false;
            int maxRetries = 30;

            Console.WriteLine($"\n[QA Step {i + 1}] Sending request to server...");

            for (int retry = 1; retry <= maxRetries; retry++)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));

                    responseModel = settings.IsServerQuery
                        ? await RequestService.DoServerQuery(requestModel, cts.Token)
                        : await RequestService.DoProviderQuery(requestModel, cts.Token);

                    if (responseModel != null && responseModel.Role != "system")
                    {
                        Console.WriteLine($"[QA Step {i + 1}] Attempt {retry}/{maxRetries} -> HTTP 200 OK (Success)");
                        success = true;
                        break;
                    }
                    else
                    {
                        string err = responseModel?.Content ?? "Unknown Error";
                        Console.WriteLine($"[QA Step {i + 1}] Attempt {retry}/{maxRetries} -> Failed Response: {err}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[QA Step {i + 1}] Attempt {retry}/{maxRetries} -> Exception: {ex.Message}");
                }

                await Task.Delay(1000);
            }

            if (!success)
            {
                Console.WriteLine($"[QA Step {i + 1}] CRITICAL: All 30 attempts failed. Skipping step.");
                logBuilder.AppendLine($"-- RESULT: SKIPPED (30/30 Attempts Failed) --\n");
                await File.AppendAllTextAsync(logFilePath, logBuilder.ToString());
                continue;
            }

            // Парсим и обрабатываем ответ ИИ без сохранения в БД
            testHistory.Add(responseModel);

            logBuilder.AppendLine($"-- AI RESPONSE --");
            logBuilder.AppendLine($"Raw Content:\n{responseModel.Content}");

            var learnedName = MessageParser.ParseTextForLearnedName(responseModel.Content);
            var datingChange = MessageParser.ParseTextForDatingChange(responseModel.Content);
            var sleepDuration = MessageParser.ParseWakeUpTime(responseModel.Content);
            var extractedDeltas = MessageParser.ExtractDeltas(responseModel.Content);

            if (learnedName != null)
            {
                settings.UserName = learnedName;
                logBuilder.AppendLine($"[TAG DETECTED] Learned Name: {learnedName}");
            }

            if (datingChange.HasValue)
            {
                settings.IsDating = datingChange.Value;
                logBuilder.AppendLine($"[TAG DETECTED] Dating State Changed: {datingChange.Value}");
            }

            if (sleepDuration.HasValue)
            {
                settings.IsSleeping = true;
                settings.WakeUpTime = simulatedTime.AddHours(sleepDuration.Value);
                logBuilder.AppendLine(
                    $"[TAG DETECTED] Sleep Initiated: {sleepDuration.Value} hours (WakeUp at {settings.WakeUpTime:yyyy-MM-dd HH:mm:ss})");
            }

            if (extractedDeltas != null)
            {
                logBuilder.AppendLine(
                    $"[DELTAS RECEIVED] Affection: {extractedDeltas.AffectionDelta:+#;-#;0}, Mood: {extractedDeltas.MoodDelta:+#;-#;0}, Energy: {extractedDeltas.EnergyDelta:+#;-#;0}, Engagement: {extractedDeltas.EngagementDelta:+#;-#;0}");
                UpdateEmotionalStates(responseModel.Content);
            }

            logBuilder.AppendLine($"-- STATES AFTER RESPONSE --");
            logBuilder.AppendLine(
                $"Affection: {settings.Affection:F1}, Mood: {settings.Mood:F1}, Energy: {settings.Energy:F1}, Engagement: {settings.Engagement:F1}");
            logBuilder.AppendLine($"RandomDailyNoise: {settings.RandomDailyNoise}\n");

            await File.AppendAllTextAsync(logFilePath, logBuilder.ToString());
        }

        Console.WriteLine("==================================================");
        Console.WriteLine("   QA TEST COMPLETED! Check qa_test_log.txt       ");
        Console.WriteLine("==================================================");
    }

 */




}