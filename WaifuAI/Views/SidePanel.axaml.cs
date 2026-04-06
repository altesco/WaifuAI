using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;

namespace WaifuAI.Views;

public class SidePanel : TemplatedControl
{
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<SettingsCard, string>(nameof(Header));

    public string Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly StyledProperty<object?> InnerContentProperty =
        AvaloniaProperty.Register<SettingsCard, object?>(nameof(InnerContent));

    [Content]
    public object? InnerContent
    {
        get => GetValue(InnerContentProperty);
        set => SetValue(InnerContentProperty, value);
    }
}