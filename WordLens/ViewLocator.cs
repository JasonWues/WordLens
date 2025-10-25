using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using WordLens.ViewModels;
using WordLens.Views;

namespace WordLens;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        return param switch
        {
            MainWindowViewModel => new MainWindowView(),
            PopupWindowViewModel => new PopupWindowView(),
            _ => throw new Exception($"Unable to create view for type: {param.GetType()}")
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}