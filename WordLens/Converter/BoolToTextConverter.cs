using Avalonia.Data.Converters;
using Avalonia.Media;

namespace WordLens.Converter
{
    public static class BoolToTextConverter
    {
        public static FuncValueConverter<bool,string,string?> BoolToParameterTextConverter { get; } = 
            new FuncValueConverter<bool, string, string?>((arg,para) =>
            {
                var paraList = para.Split('|');
                
                if (arg)
                {
                    return paraList[0];
                }
                return paraList[1];
            });
    }
}