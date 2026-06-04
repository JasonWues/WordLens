using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Semi.Avalonia;
using WordLens.Services;

namespace WordLens.Infrastructure.Avalonia;

public sealed class LocalizationService : ILocalizationService
{
    private const string DefaultCulture = "zh-CN";

    public event EventHandler? CultureChanged;

    public string CurrentCulture { get; private set; } = DefaultCulture;

    public LocalizationService()
    {
        if (Application.Current is { } application)
            ReplaceLocalizationResources(application, CurrentCulture);
    }

    public void ApplyCulture(string? cultureName)
    {
        var culture = NormalizeCulture(cultureName);
        CurrentCulture = culture;

        if (Application.Current is { } application)
        {
            SemiTheme.OverrideLocaleResources(application, new CultureInfo(GetSemiCulture(culture)));
            ReplaceLocalizationResources(application, culture);
        }

        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        if (Application.Current?.TryGetResource(key, null, out var value) == true &&
            value is string text)
        {
            return text;
        }

        return key;
    }

    public string GetString(string key, params object[] args)
    {
        var format = GetString(key);
        return args.Length == 0 ? format : string.Format(CultureInfo.CurrentCulture, format, args);
    }

    public static string NormalizeCulture(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
            return DefaultCulture;

        return cultureName.Trim() switch
        {
            "zh" or "zh-Hans" or "zh-CN" => "zh-CN",
            "en" or "en-US" or "en-GB" => "en",
            "ja" or "ja-JP" => "ja",
            _ => DefaultCulture
        };
    }

    private static string GetSemiCulture(string culture)
    {
        return culture switch
        {
            "en" => "en-US",
            "ja" => "ja-JP",
            _ => culture
        };
    }

    private void ReplaceLocalizationResources(Application application, string culture)
    {
        if (application.Resources is not ResourceDictionary resources)
            return;

        LocalizationResources.ApplyTo(resources, culture);
    }
}
