using System;
using System.Linq;
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
    private readonly ILocalizationService? _localizationService;
    private readonly IWindowManagerService _windowManager;
    private int _recognitionVersion;
    private object[] _statusTextArgs = Array.Empty<object>();
    private string _statusTextKey = "Ocr_StatusWaiting";

    [ObservableProperty] private bool hasError;

    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private string recognizedText = "";

    [ObservableProperty] private WriteableBitmap? screenshot;

    [ObservableProperty] private string statusText = "等待识别";

    public OcrResultViewModel()
    {
        _ocrService = null!;
        _localizationService = null;
        _windowManager = null!;
        _logger = null!;
    }

    public OcrResultViewModel(
        IOcrService ocrService,
        IWindowManagerService windowManager,
        ILocalizationService localizationService,
        ILogger<OcrResultViewModel> logger)
    {
        _ocrService = ocrService;
        _windowManager = windowManager;
        _localizationService = localizationService;
        _logger = logger;
        SetStatusText("Ocr_StatusWaiting");
        _localizationService.CultureChanged += OnCultureChanged;
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
        SetStatusText(
            HasRecognizedText ? "Ocr_StatusDoneFormat" : "Ocr_StatusReady",
            RecognizedText.Length);

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
        SetStatusText("Ocr_StatusRecognizing");

        try
        {
            _logger.ZLogInformation($"开始重新识别 OCR 截图");

            var text = await _ocrService.RecognizeTextAsync(Screenshot, "auto");

            if (recognitionVersion != _recognitionVersion)
                return;

            RecognizedText = text?.Trim() ?? "";

            if (HasRecognizedText)
                SetStatusText("Ocr_StatusDoneFormat", RecognizedText.Length);
            else
                SetStatusText("Ocr_StatusNoText");

            _logger.ZLogInformation($"OCR 识别完成，文本长度: {RecognizedText.Length}");
        }
        catch (Exception ex)
        {
            if (recognitionVersion != _recognitionVersion)
                return;

            HasError = true;
            SetStatusText("Ocr_StatusFailedFormat", ex.Message);
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

    private void SetStatusText(string resourceKey, params object[] args)
    {
        _statusTextKey = resourceKey;
        _statusTextArgs = args;
        StatusText = _localizationService?.GetString(resourceKey, args) ?? GetFallbackStatusText(resourceKey, args);
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        StatusText = _localizationService?.GetString(_statusTextKey, _statusTextArgs)
                     ?? GetFallbackStatusText(_statusTextKey, _statusTextArgs);
    }

    private static string GetFallbackStatusText(string resourceKey, object[] args)
    {
        return resourceKey switch
        {
            "Ocr_StatusReady" => "准备识别",
            "Ocr_StatusRecognizing" => "正在识别...",
            "Ocr_StatusDoneFormat" => $"识别完成，{args.FirstOrDefault() ?? 0} 个字符",
            "Ocr_StatusNoText" => "未识别到文字",
            "Ocr_StatusFailedFormat" => $"识别失败：{args.FirstOrDefault() ?? string.Empty}",
            _ => "等待识别"
        };
    }
}
