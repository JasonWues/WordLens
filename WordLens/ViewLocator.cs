using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using WordLens.ViewModels;
using WordLens.Views;
using WordLens.Views.Settings;

namespace WordLens;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        Control control = param switch
        {
            null => throw new ArgumentNullException(nameof(param)),
            MainWindowViewModel => new MainWindowView(),
            PopupWindowViewModel => new PopupWindowView(),
            GeneralSettingsViewModel => new GeneralSettingsView(),
            TranslationSettingsViewModel => new TranslationSettingsView(),
            OcrSettingsViewModel => new OcrSettingsView(),
            TtsSettingsViewModel => new TtsSettingsView(),
            NetworkSettingsViewModel => new NetworkSettingsView(),
            TranslationHistoryViewModel => new TranslationHistoryView(),
            AboutViewModel => new AboutView(),
            _ => throw new Exception($"Unable to create view for type: {param.GetType()}")
        };

        control.DataContext = param;
        return control;
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
