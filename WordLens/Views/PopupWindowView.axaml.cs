using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using WordLens.ViewModels;

namespace WordLens.Views
{
    public partial class PopupWindowView : Window
    {
        public PopupWindowView()
        {
            InitializeComponent();
        }

        private async void CopySource_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is PopupWindowViewModel vm)
            {
                await CopyToClipboardAsync(vm.SourceText);
            }
        }

        private async void CopyTranslation_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string text } button)
            {
                await CopyToClipboardAsync(text);
            }
        }

        private async Task CopyToClipboardAsync(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(text);
            }
        }
    }
}