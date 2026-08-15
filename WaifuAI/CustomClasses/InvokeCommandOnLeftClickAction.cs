using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using System.Windows.Input;
using Avalonia;

namespace WaifuAI.CustomClasses;

public class InvokeCommandOnLeftClickAction : AvaloniaObject, IAction
{
    public static readonly StyledProperty<ICommand> CommandProperty =
        AvaloniaProperty.Register<InvokeCommandOnLeftClickAction, ICommand>(nameof(Command));

    public static readonly StyledProperty<object> CommandParameterProperty =
        AvaloniaProperty.Register<InvokeCommandOnLeftClickAction, object>(nameof(CommandParameter));

    public ICommand Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public object Execute(object? sender, object? parameter)
    {
        // Проверяем, что событие пришло от указателя и нажата именно ПРАВАЯ кнопка
        if (parameter is PointerPressedEventArgs e && 
            e.GetCurrentPoint(null).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
        {
            if (Command?.CanExecute(CommandParameter) == true)
            {
                Command.Execute(CommandParameter);
            }
        }
        return true;
    }
}
