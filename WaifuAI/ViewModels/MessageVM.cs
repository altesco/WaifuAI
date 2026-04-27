using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using WaifuAI.Models;

namespace WaifuAI.ViewModels;

public partial class MessageVM : ObservableObject
{
    public Message? MessageModel { get; set; }
    
    // свойства того что было процитировано из другого сообщения
    [ObservableProperty] private string? _quote;
    [ObservableProperty] private int _quoteStart;
    [ObservableProperty] private int _quoteEnd;
    [ObservableProperty] private bool? _isReplied; 

    // сообщение, на которое отвечает this
    [ObservableProperty] private MessageVM? _replyMessage; 

    // cообщения, которые отвечают на this
    public List<MessageVM> ReplyingMessages { get; } = [];
    
    // выделение для подсветки
    [ObservableProperty] private int _selectionStart;
    [ObservableProperty] private int _selectionEnd;
    [ObservableProperty] private bool _isHighlighted;

    [ObservableProperty] private bool _isFailed;
}