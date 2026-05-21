using System.Windows.Input;
using Avalonia;
using Avalonia.Data;
using Avalonia.Input;

namespace WordLens.Behaviors;

public class TapCommand : AvaloniaObject
{
    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<TapCommand, InputElement, ICommand?>(
            "Command",
            default,
            false,
            BindingMode.OneWay);

    public static readonly AttachedProperty<object?> CommandParameterProperty =
        AvaloniaProperty.RegisterAttached<TapCommand, InputElement, object?>(
            "CommandParameter",
            default,
            false,
            BindingMode.OneWay);

    static TapCommand()
    {
        CommandProperty.Changed.AddClassHandler<InputElement>(OnCommandChanged);
    }

    public static void SetCommand(InputElement element, ICommand? command)
    {
        element.SetValue(CommandProperty, command);
    }

    public static ICommand? GetCommand(InputElement element)
    {
        return element.GetValue(CommandProperty);
    }

    public static void SetCommandParameter(InputElement element, object? parameter)
    {
        element.SetValue(CommandParameterProperty, parameter);
    }

    public static object? GetCommandParameter(InputElement element)
    {
        return element.GetValue(CommandParameterProperty);
    }

    private static void OnCommandChanged(InputElement element, AvaloniaPropertyChangedEventArgs args)
    {
        element.RemoveHandler(InputElement.TappedEvent, OnTapped);

        if (args.NewValue is ICommand)
            element.AddHandler(InputElement.TappedEvent, OnTapped);
    }

    private static void OnTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not InputElement element)
            return;

        var command = GetCommand(element);
        var parameter = GetCommandParameter(element);

        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
    }
}
