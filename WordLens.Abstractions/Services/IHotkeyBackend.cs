using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WordLens.Models;

namespace WordLens.Services;

public sealed record HotkeyRegistration(int Id, string Name, HotkeyConfig Config);

public sealed class HotkeyPressedEventArgs(int id) : EventArgs
{
    public int Id { get; } = id;
}

public interface IHotkeyBackend : IDisposable
{
    event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    Task RegisterAsync(IReadOnlyCollection<HotkeyRegistration> registrations);

    void UnregisterAll();
}
