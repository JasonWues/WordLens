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

        public PopupWindowViewModel()
        {

        }

        public PopupWindowViewModel(TranslationService translationService)
        {
            _translationService = translationService;
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
    }
}