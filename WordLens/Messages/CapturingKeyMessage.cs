using Avalonia.Input;

namespace WordLens.Messages;

public class CapturingKeyMessage(KeyEventArgs keyEventArgs)
{
    public KeyEventArgs KeyEventArgs { get; } = keyEventArgs;
}