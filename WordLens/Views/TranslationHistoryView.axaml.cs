using System;
using Avalonia.Controls;
using WordLens.ViewModels;

namespace WordLens.Views;

public partial class TranslationHistoryView : Window
{
    public TranslationHistoryView()
    {
        InitializeComponent();
        
        // 窗口加载时初始化ViewModel
        Loaded += async (s, e) =>
        {
            if (DataContext is TranslationHistoryViewModel vm)
            {
                await vm.InitializeAsync();
            }
        };
    }
}