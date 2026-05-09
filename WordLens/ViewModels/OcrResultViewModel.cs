using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WordLens.Services;
using ZLogger;

namespace WordLens.ViewModels;

/// <summary>
///     OCR 结果窗口的 ViewModel，管理截图预览、文字识别和翻译入口。
/// </summary>
public partial class OcrResultViewModel : ViewModelBase
{
    private readonly ILogger<OcrResultViewModel> _logger;
    private readonly IOcrService _ocrService;
    private readonly IWindowManagerService _windowManager;
    private int _recognitionVersion;

    [ObservableProperty] private bool hasError;

    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private string recognizedText = "";

    [ObservableProperty] private WriteableBitmap? screenshot;

    [ObservableProperty] private string statusText = "等待识别";

    public OcrResultViewModel()
    {
        _ocrService = null!;
        _windowManager = null!;
        _logger = null!;
    }

    public OcrResultViewModel(
        IOcrService ocrService,
        IWindowManagerService windowManager,
        ILogger<OcrResultViewModel> logger)
    {
        _ocrService = ocrService;
        _windowManager = windowManager;
        _logger = logger;
    }

    public bool CanReRecognize => Screenshot != null && !IsBusy;

    public bool CanTranslate => HasRecognizedText && !IsBusy;

    public bool HasRecognizedText => !string.IsNullOrWhiteSpace(RecognizedText);

    public bool HasScreenshot => Screenshot != null;

    public void LoadScreenshot(WriteableBitmap bitmap, string? initialText = null)
    {
        _recognitionVersion++;
        Screenshot = bitmap;
        RecognizedText = initialText?.Trim() ?? "";
        HasError = false;
        StatusText = HasRecognizedText ? $"识别完成，{RecognizedText.Length} 个字符" : "准备识别";

        if (!HasRecognizedText)
            _ = ReRecognizeAsync();
    }

    partial void OnIsBusyChanged(bool value)
    {
        NotifyCommandStateChanged();
    }

    partial void OnRecognizedTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasRecognizedText));
        NotifyCommandStateChanged();
    }

    partial void OnScreenshotChanged(WriteableBitmap? value)
    {
        OnPropertyChanged(nameof(HasScreenshot));
        NotifyCommandStateChanged();
    }

    [RelayCommand(CanExecute = nameof(CanReRecognize))]
    private async Task ReRecognizeAsync()
    {
        if (Screenshot == null)
            return;

        var recognitionVersion = ++_recognitionVersion;

        IsBusy = true;
        HasError = false;
        StatusText = "正在识别...";

        try
        {
            _logger.ZLogInformation($"开始重新识别 OCR 截图");

            var text = await _ocrService.RecognizeTextAsync(Screenshot, "auto");

            if (recognitionVersion != _recognitionVersion)
                return;

            RecognizedText = text?.Trim() ?? "";

            StatusText = HasRecognizedText
                ? $"识别完成，{RecognizedText.Length} 个字符"
                : "未识别到文字";

            _logger.ZLogInformation($"OCR 识别完成，文本长度: {RecognizedText.Length}");
        }
        catch (Exception ex)
        {
            if (recognitionVersion != _recognitionVersion)
                return;

            HasError = true;
            StatusText = $"识别失败：{ex.Message}";
            _logger.ZLogError(ex, $"OCR 识别失败: {ex.Message}");
        }
        finally
        {
            if (recognitionVersion == _recognitionVersion)
                IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanTranslate))]
    private async Task TranslateAsync()
    {
        var text = RecognizedText.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        await _windowManager.ShowTranslationWindowAsync(text);
    }

    private void NotifyCommandStateChanged()
    {
        ReRecognizeCommand.NotifyCanExecuteChanged();
        TranslateCommand.NotifyCanExecuteChanged();
    }
}
