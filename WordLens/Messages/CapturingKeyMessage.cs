using SharpHook.Data;

namespace WordLens.Messages;

public class CapturingKeyMessage(KeyCode keyCode, EventMask modifiers)
{
    public KeyCode KeyCode { get; } = keyCode;

    public EventMask Modifiers { get; } = modifiers;

    public bool Handled { get; set; }
}
