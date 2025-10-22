using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WordLens.Models;
using WordLens.Services;
using ZLogger;

namespace WordLens.ViewModels
{
    public partial class PopupWindowViewModel : ViewModelBase
    {
        private readonly TranslationService _translationService;
        private readonly ILogger<PopupWindowViewModel> _logger;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string? sourceText;

        [ObservableProperty]
        private ObservableCollection<TranslationResult> translationResults = new();

        [ObservableProperty]
        private bool isTopmost;

        public bool CanCopySource => !string.IsNullOrWhiteSpace(SourceText);
        
        public bool HasTranslationResults => TranslationResults.Count > 0;

        public PopupWindowViewModel()
        {
            _translationService = null!;
            _logger = null!;
        }

        public PopupWindowViewModel(TranslationService translationService, ILogger<PopupWindowViewModel> logger)
        {
            _translationService = translationService;
            _logger = logger;
        }

        partial void OnSourceTextChanged(string? value)
        {
            OnPropertyChanged(nameof(CanCopySource));
        }

        partial void OnTranslationResultsChanged(ObservableCollection<TranslationResult> value)
        {
            OnPropertyChanged(nameof(HasTranslationResults));
        }

        [RelayCommand]
        public async Task TranslateAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(SourceText))
                return;

            IsBusy = true;
            TranslationResults.Clear();

            try
            {
                _logger.ZLogInformation($"开始翻译请求");

                var results = await _translationService.TranslateAsync(SourceText, cancellationToken);

                foreach (var result in results)
                {
                    TranslationResults.Add(result);
                }

                _logger.ZLogInformation($"翻译结果已添加到UI，共 {results.Count} 个");
            }
            catch (System.Exception ex)
            {
                _logger.ZLogError(ex, $"翻译过程中发生异常");
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
            TranslationResults.Clear();
        }
    }
}