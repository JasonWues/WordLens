using Avalonia.Data.Converters;

namespace WordLens.Converter;

public class BoolToOpacityConverter
{
    public static FuncValueConverter<bool, double?> BoolToOpacityValueConverter { get; } =
        new((arg) =>
        {
            if (arg)
            {
                return 100;
            }
            else
            {
                return 0;
            }
        });
}