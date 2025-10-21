using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WordLens.Services;

namespace WordLens.ViewModels
{
    public partial class PopupWindowViewModel : ViewModelBase
    {
        readonly private TranslationService _translationService;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string? sourceText;

        [ObservableProperty]
        private string? translatedText;

        [ObservableProperty]
        private bool isTopmost;

        public bool CanCopySource => !string.IsNullOrWhiteSpace(SourceText);
        
        public bool CanCopyTranslation => !string.IsNullOrWhiteSpace(TranslatedText);

        public PopupWindowViewModel()
        {

        }

        public PopupWindowViewModel(TranslationService translationService)
        {
            _translationService = translationService;
        }

        partial void OnSourceTextChanged(string? value)
        {
            OnPropertyChanged(nameof(CanCopySource));
        }

        partial void OnTranslatedTextChanged(string? value)
        {
            OnPropertyChanged(nameof(CanCopyTranslation));
        }

        [RelayCommand]
        public async Task TranslateAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(SourceText))
                return;

            IsBusy = true;
            try
            {
                TranslatedText = await _translationService.TranslateAsync(SourceText, cancellationToken);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void ToggleTopmost()
        {
            IsTopmost = !IsTopmost;
        }

        [RelayCommand]
        public void ClearSource()
        {
            SourceText = string.Empty;
            TranslatedText = string.Empty;
        }
    }
}