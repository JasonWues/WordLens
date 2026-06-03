using System;

namespace WordLens.Services;

public interface ILocalizationService
{
    event EventHandler? CultureChanged;

    string CurrentCulture { get; }

    void ApplyCulture(string? cultureName);

    string GetString(string key);

    string GetString(string key, params object[] args);
}
