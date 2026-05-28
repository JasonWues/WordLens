using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Semi.Avalonia;

namespace WordLens.Infrastructure.Avalonia;

public sealed class AvaloniaThemeService
{
    public const string AppFontFamilyResourceKey = "AppFontFamily";

    public void ApplyLocale(string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName) || Application.Current == null)
            return;

        SemiTheme.OverrideLocaleResources(Application.Current, new CultureInfo(cultureName));
    }

    public void ApplyFontFamily(string? fontFamilyName)
    {
        if (Application.Current == null)
            return;

        Application.Current.Resources[AppFontFamilyResourceKey] = ResolveFontFamily(fontFamilyName);
    }

    public static FontFamily ResolveFontFamily(string? fontFamilyName)
    {
        if (string.IsNullOrWhiteSpace(fontFamilyName))
            return FontFamily.Default;

        try
        {
            return FontFamily.Parse(fontFamilyName.Trim());
        }
        catch
        {
            return FontFamily.Default;
        }
    }

    public static string GetDefaultFontDisplayName()
    {
        return FontManager.Current.DefaultFontFamily.Name;
    }

    public static string[] GetSystemFontFamilyNames()
    {
        return FontManager.Current.SystemFonts
            .Select(static fontFamily => fontFamily.Name)
            .Where(static fontFamily => !string.IsNullOrWhiteSpace(fontFamily))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static fontFamily => fontFamily, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }
}
