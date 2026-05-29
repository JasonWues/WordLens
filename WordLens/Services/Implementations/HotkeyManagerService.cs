using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WordLens.Abstractions.Models;
using WordLens.Abstractions.Services;
using WordLens.Messages;
using WordLens.Models;
using ZLogger;

namespace WordLens.Services.Implementations;

/// <summary>
///     热键管理服务
/// </summary>
public class HotkeyManagerService : IHotkeyManagerService
{
    private const int TranslationHotkeyId = 1;
    private const int OcrHotkeyId = 2;

    private readonly IHotkeyBackend _hotkeyBackend;
    private readonly ILogger<HotkeyManagerService> _logger;
    private readonly ISelectionService _selectionService;
    private readonly ISettingsService _settingsService;
    private HotkeyConfig _ocrHotkey = HotkeyConfig.DefaultOcr();
    private HotkeyConfig _translationHotkey = HotkeyConfig.Default();

    public HotkeyManagerService(
        IHotkeyBackend hotkeyBackend,
        ISettingsService settingsService,
        ISelectionService selectionService,
        ILogger<HotkeyManagerService> logger)
    {
        _hotkeyBackend = hotkeyBackend;
        _settingsService = settingsService;
        _selectionService = selectionService;
        _logger = logger;
    }

    public async Task StartAsync()
    {
        var settings = await _settingsService.LoadAsync();
        await StartAsync(settings);
    }

    public async Task StartAsync(AppSettings settings)
    {
        _logger.ZLogInformation($"热键管理服务启动");

        _translationHotkey = settings.Hotkey;
        _ocrHotkey = settings.OcrHotkey;

        _logger.ZLogInformation($"翻译热键配置: Modifiers={_translationHotkey.Modifiers}, Key={_translationHotkey.Key}");
        _logger.ZLogInformation($"OCR热键配置: Modifiers={_ocrHotkey.Modifiers}, Key={_ocrHotkey.Key}");

        _hotkeyBackend.HotkeyPressed += OnHotkeyPressed;
        await RegisterHotkeysAsync();

        _logger.ZLogInformation($"热键管理服务启动完成");
    }

    /// <summary>
    ///     重新加载快捷键配置
    /// </summary>
    public async Task ReloadConfigAsync()
    {
        var settings = await _settingsService.LoadAsync();
        await ReloadConfigAsync(settings);
    }

    public async Task ReloadConfigAsync(AppSettings settings)
    {
        _translationHotkey = settings.Hotkey;
        _ocrHotkey = settings.OcrHotkey;

        await RegisterHotkeysAsync();

        _logger.ZLogInformation($"热键配置已重新加载");
    }

    /// <summary>
    ///     翻译热键触发处理
    /// </summary>
    private void OnTranslationHotkeyTriggered()
    {
        var text = _selectionService.GetSelectedTex();
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.ZLogWarning($"未选择文本，忽略翻译热键");
            return;
        }

        _logger.ZLogInformation($"获取到选中文本，长度: {text.Length}");
        WeakReferenceMessenger.Default.Send(new TriggerTranslationMessage(text), "text");
    }

    /// <summary>
    ///     OCR热键触发处理
    /// </summary>
    private void OnOcrHotkeyTriggered()
    {
        _logger.ZLogInformation($"OCR热键被触发，打开屏幕截图窗口");

        // 发送消息打开屏幕捕获窗口
        WeakReferenceMessenger.Default.Send(new TriggerTranslationMessage(string.Empty), "ocr");
    }

    private Task RegisterHotkeysAsync()
    {
        return _hotkeyBackend.RegisterAsync(new[]
        {
            new HotkeyRegistration(TranslationHotkeyId, "翻译", _translationHotkey),
            new HotkeyRegistration(OcrHotkeyId, "OCR", _ocrHotkey)
        });
    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        switch (e.Id)
        {
            case TranslationHotkeyId:
                _logger.ZLogInformation($"翻译热键被触发");
                _ = Task.Run(OnTranslationHotkeyTriggered);
                break;
            case OcrHotkeyId:
                _logger.ZLogInformation($"OCR热键被触发");
                _ = Task.Run(OnOcrHotkeyTriggered);
                break;
        }
    }

    public void Dispose()
    {
        _hotkeyBackend.HotkeyPressed -= OnHotkeyPressed;
        _hotkeyBackend.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask;
    }
}
