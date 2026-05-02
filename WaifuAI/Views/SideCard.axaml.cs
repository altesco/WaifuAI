using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;

namespace WaifuAI.Views;

public class SideCard : TemplatedControl
{
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<SettingsCard, string>(nameof(Header));

    public string Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<SettingsCard, string>(nameof(Description));

    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<SettingsCard, object?>(nameof(Icon));

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly StyledProperty<object?> InnerContentProperty =
        AvaloniaProperty.Register<SettingsCard, object?>(nameof(InnerContent));

    [Content]
    public object? InnerContent
    {
        get => GetValue(InnerContentProperty);
        set => SetValue(InnerContentProperty, value);
    }

    public static readonly StyledProperty<bool> IsBigTextBoxProperty =
        AvaloniaProperty.Register<SettingsCard, bool>(nameof(IsBigTextBox));

    public bool IsBigTextBox
    {
        get => GetValue(IsBigTextBoxProperty);
        set => SetValue(IsBigTextBoxProperty, value);
    }
}