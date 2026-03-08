using CommunityToolkit.Mvvm.ComponentModel;
using WaifuAI.Models;

namespace WaifuAI.ViewModels;

public partial class MessageVM : ObservableObject
{
    public Message? MessageModel { get; set; }
    
    [ObservableProperty] private string _time = string.Empty;
    [ObservableProperty] private bool _isFailed;
    [ObservableProperty] private int _selectionStart;
    [ObservableProperty] private int _selectionEnd;
    [ObservableProperty] private string? _quote;
    [ObservableProperty] private int _quoteStart;
    [ObservableProperty] private int _quoteEnd;
    [ObservableProperty] private bool? _isReplying;
}