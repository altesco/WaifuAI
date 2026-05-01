using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WaifuAI.Models;
using WaifuAI.Services;

namespace WaifuAI.ViewModels;

public partial class MessageVM : ObservableObject
{
    public MessageVM(Message messageModel)
    {
        MessageModel = messageModel;
        Quote = messageModel.Quote;
        QuoteStart = messageModel.QuoteStart;
        QuoteEnd = messageModel.QuoteEnd;
        IsReplied = messageModel.IsReplied;
    }

    public Message MessageModel { get; set; }
    
    // свойства того что было процитировано из другого сообщения
    [ObservableProperty] private string? _quote;
    [ObservableProperty] private int _quoteStart;
    [ObservableProperty] private int _quoteEnd;
    [ObservableProperty] private bool? _isReplied; 

    // сообщение, на которое отвечает this
    [ObservableProperty] private MessageVM? _replyMessage; 

    // cообщения, которые отвечают на this
    public ObservableCollection<MessageVM> ReplyingMessages { get; } = [];
    
    // выделение для подсветки
    [ObservableProperty] private int _selectionStart;
    [ObservableProperty] private int _selectionEnd;
    [ObservableProperty] private bool _isHighlighted;

    [ObservableProperty] private bool _isFailed;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (SettingsVM.Instance.IsAppInitializing || 
            IsFailed ||
            e.PropertyName is 
                nameof(SelectionStart) or
                nameof(SelectionEnd) or
                nameof(IsHighlighted))
            return;
        MessageModel.Quote = Quote;
        MessageModel.QuoteStart = QuoteStart;
        MessageModel.QuoteEnd = QuoteEnd;
        MessageModel.IsReplied = IsReplied;
        MessageModel.ReplyMessageId = ReplyMessage?.MessageModel.Id;
        DatabaseService.HistoryDb.InsertOrReplaceAsync(MessageModel);
    }
}