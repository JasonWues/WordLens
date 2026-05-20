using System.Globalization;
using Avalonia;
using Semi.Avalonia;

namespace WordLens.Services.Implementations;

public sealed class AvaloniaThemeService
{
    public void ApplyLocale(string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName) || Application.Current == null)
            return;

        SemiTheme.OverrideLocaleResources(Application.Current, new CultureInfo(cultureName));
    }
}
