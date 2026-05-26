using Avalonia.Input;
using SharpHook.Data;
using WordLens.Abstractions.Models;
using WordLens.Util;
using WordLens.ViewModels;

namespace WordLens.Test;

public class HotkeyTests
{
    [Fact]
    public void DefaultHotkeys_UseConfiguredKeys()
    {
        var translation = HotkeyConfig.Default();
        var ocr = HotkeyConfig.DefaultOcr();

        Assert.Equal(EventMask.LeftCtrl | EventMask.LeftShift, translation.Modifiers);
        Assert.Equal(KeyCode.VcT, translation.Key);
        Assert.Equal(EventMask.LeftCtrl | EventMask.LeftShift, ocr.Modifiers);
        Assert.Equal(KeyCode.VcW, ocr.Key);
    }

    [Fact]
    public void ConvertToKeyCode_MapsSupportedAvaloniaKeys()
    {
        Assert.Equal(KeyCode.VcA, KeyCodeUtil.ConvertToKeyCode(Key.A));
        Assert.Equal(KeyCode.Vc0, KeyCodeUtil.ConvertToKeyCode(Key.D0));
        Assert.Equal(KeyCode.VcF12, KeyCodeUtil.ConvertToKeyCode(Key.F12));
        Assert.Equal(KeyCode.VcSpace, KeyCodeUtil.ConvertToKeyCode(Key.Space));
        Assert.Equal(KeyCode.VcEnter, KeyCodeUtil.ConvertToKeyCode(Key.Enter));
    }

    [Fact]
    public void ConvertToKeyCode_ReturnsUndefined_ForUnsupportedAvaloniaKeys()
    {
        Assert.Equal(KeyCode.VcUndefined, KeyCodeUtil.ConvertToKeyCode(Key.Escape));
    }

    [Fact]
    public void ConvertToEventMask_MapsAvaloniaModifiersToSharpHookFlags()
    {
        var mask = KeyCodeUtil.ConvertToEventMask(KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Meta);

        Assert.True(mask.HasFlag(EventMask.LeftCtrl));
        Assert.True(mask.HasFlag(EventMask.LeftShift));
        Assert.True(mask.HasFlag(EventMask.LeftMeta));
        Assert.False(mask.HasFlag(EventMask.LeftAlt));
    }

    [Fact]
    public void GetKeyName_ReturnsFriendlyNamesOrFallback()
    {
        Assert.Equal("A", KeyCodeUtil.GetKeyName(KeyCode.VcA));
        Assert.Equal("Space", KeyCodeUtil.GetKeyName(KeyCode.VcSpace));
        Assert.Equal("Enter", KeyCodeUtil.GetKeyName(KeyCode.VcEnter));
        Assert.Equal(KeyCode.VcEscape.ToString(), KeyCodeUtil.GetKeyName(KeyCode.VcEscape));
    }

    [Fact]
    public void CloneHotkeyConfig_ReturnsIndependentCopy()
    {
        var source = new HotkeyConfig
        {
            Modifiers = EventMask.LeftCtrl | EventMask.LeftAlt,
            Key = KeyCode.VcQ
        };

        var clone = GeneralSettingsViewModel.CloneHotkeyConfig(source);
        source.Key = KeyCode.VcW;

        Assert.NotSame(source, clone);
        Assert.Equal(EventMask.LeftCtrl | EventMask.LeftAlt, clone.Modifiers);
        Assert.Equal(KeyCode.VcQ, clone.Key);
    }
}
