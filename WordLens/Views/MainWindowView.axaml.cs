using System;
using Avalonia.Controls;
using Avalonia.Input;
using WordLens.ViewModels;

namespace WordLens.Views
{
    public partial class MainWindowView : Window
    {
        public MainWindowView()
        {
            InitializeComponent();
            
            // 订阅窗口加载事件
            Opened += async (s, e) =>
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    await vm.InitializeAsync();
                }
            };
            
            // 订阅 KeyDown 事件用于快捷键捕获
            KeyDown += OnWindowKeyDown;
        }

        private void OnWindowKeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is MainWindowViewModel mainVm &&
                mainVm.SettingsViewModel.IsCapturingHotkey)
            {
                mainVm.SettingsViewModel.CaptureKey(e);
                e.Handled = true;
            }
        }
    }
}