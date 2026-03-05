using CommunityToolkit.Mvvm.ComponentModel;
using WaifuAI.Models;

namespace WaifuAI.ViewModels;

public partial class MessageVM : ObservableObject
{
    public Message? MessageModel { get; set; }
    
    [ObservableProperty] private string _time = string.Empty;
    [ObservableProperty] private bool _isFailed;
}