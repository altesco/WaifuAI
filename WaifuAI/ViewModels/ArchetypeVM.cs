using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Echoes;
using WaifuAI.Models;

namespace WaifuAI.ViewModels;

public partial class ArchetypeVM : ObservableObject
{
    public string Name { get; set; } = string.Empty;

    public TranslationUnit DescriptionTranslation { get; init; }

    //[ObservableProperty] private string _description = string.Empty;
    public string Description => DescriptionTranslation?.CurrentValue ?? string.Empty;
    
    public string Emoji { get; set; } = "👤";
    public Color Color { get; set; } = Colors.Blue;
    
    [ObservableProperty] private string _prompt = string.Empty;

    public MoodVector BaseMoodVector { get; set; }

    public float BreakUpAffection { get; set; }
    public float BreakUpMood { get; set; }

    public ArchetypeSensitivity Sensitivity { get; set; }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(Description));
    }
}
