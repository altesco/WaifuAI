using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WaifuAI.ViewModels;

public partial class ArchetypeVM : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Emoji { get; set; } = "👤";
    public Color Color { get; set; } = Colors.Blue;
    [ObservableProperty] private string _prompt = string.Empty;
}
