using SharpHook.Data;

namespace WordLens.Abstractions.Models;

public class HotkeyConfig
{
    public EventMask Modifiers { get; set; }

    public KeyCode Key { get; set; }

    public static HotkeyConfig Default()
    {
        return new HotkeyConfig
        {
            Modifiers = EventMask.LeftCtrl | EventMask.LeftShift,
            Key = KeyCode.VcT
        };
    }

    public static HotkeyConfig DefaultOcr()
    {
        return new HotkeyConfig
        {
            Modifiers = EventMask.LeftCtrl | EventMask.LeftShift,
            Key = KeyCode.VcW
        };
    }
}
