using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using WordLens.ViewModels;
using WordLens.Views;
using WordLens.Views.Settings;

namespace WordLens;

public class ViewLocator : IDataTemplate
{
    private readonly Dictionary<ViewModelBase, Control> _cachedSettingViews = new();

    public Control? Build(object? param)
    {
        Control control = param switch
        {
            null => throw new ArgumentNullException(nameof(param)),
            MainWindowViewModel => new MainWindowView(),
            PopupWindowViewModel => new PopupWindowView(),
            GeneralSettingsViewModel viewModel => GetOrCreateCachedView(viewModel, static () => new GeneralSettingsView()),
            TranslationSettingsViewModel viewModel => GetOrCreateCachedView(viewModel, static () => new TranslationSettingsView()),
            OcrSettingsViewModel viewModel => GetOrCreateCachedView(viewModel, static () => new OcrSettingsView()),
            TtsSettingsViewModel viewModel => GetOrCreateCachedView(viewModel, static () => new TtsSettingsView()),
            NetworkSettingsViewModel viewModel => GetOrCreateCachedView(viewModel, static () => new NetworkSettingsView()),
            TranslationHistoryViewModel viewModel => GetOrCreateCachedView(viewModel, static () => new TranslationHistoryView()),
            AboutViewModel viewModel => GetOrCreateCachedView(viewModel, static () => new AboutView()),
            _ => throw new Exception($"Unable to create view for type: {param.GetType()}")
        };

        control.DataContext = param;
        return control;
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }

    private Control GetOrCreateCachedView(ViewModelBase viewModel, Func<Control> factory)
    {
        if (_cachedSettingViews.TryGetValue(viewModel, out var view))
            return view;

        view = factory();
        _cachedSettingViews.Add(viewModel, view);
        return view;
    }
}
