using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using SharpHook;
using WordLens.Messages;
using WordLens.Models;
using ZLogger;

namespace WordLens.Services
{
    public interface IHotkeyManagerService : IDisposable,IAsyncDisposable
    {
        Task StartAsync();
        Task ReloadConfigAsync();
        
        
    }

    /// <summary>
    /// 热键管理服务
    /// </summary>
    public class HotkeyManagerService : IHotkeyManagerService
    {
        private readonly IGlobalHook _globalHook;
        private readonly ISettingsService _settingsService;
        private readonly ISelectionService _selectionService;
        private readonly ILogger<HotkeyManagerService> _logger;
        
        private HotkeyConfig _translationHotkey = HotkeyConfig.Default();
        private HotkeyConfig _ocrHotkey = HotkeyConfig.Default();

        public HotkeyManagerService(
            IGlobalHook globalHook,
            ISettingsService settingsService,
            ISelectionService selectionService,
            ILogger<HotkeyManagerService> logger)
        {
            _globalHook = globalHook;
            _settingsService = settingsService;
            _selectionService = selectionService;
            _logger = logger;
        }

        public async Task StartAsync()
        {
            _logger.ZLogInformation($"热键管理服务启动");
            
            // 加载快捷键配置
            var settings = await _settingsService.LoadAsync();
            _translationHotkey = settings.Hotkey;
            _ocrHotkey = settings.OcrHotkey;

            _logger.ZLogInformation($"翻译热键配置: Modifiers={_translationHotkey.Modifiers}, Key={_translationHotkey.Key}");
            _logger.ZLogInformation($"OCR热键配置: Modifiers={_ocrHotkey.Modifiers}, Key={_ocrHotkey.Key}");
            
            _globalHook.KeyPressed += OnGlobalKeyPressed;
            
            // 启动 GlobalHook
            await _globalHook.RunAsync();
            
            _logger.ZLogInformation($"热键管理服务启动完成");
        }

        /// <summary>
        /// 重新加载快捷键配置
        /// </summary>
        public async Task ReloadConfigAsync()
        {
            var settings = await _settingsService.LoadAsync();
            _translationHotkey = settings.Hotkey;
            _ocrHotkey = settings.OcrHotkey;
            _logger.ZLogInformation($"热键配置已重新加载");
        }

        /// <summary>
        /// 全局键盘事件处理
        /// </summary>
        private void OnGlobalKeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            // 检查翻译快捷键
            if (IsHotkeyMatch(e, _translationHotkey))
            {
                _logger.ZLogInformation($"翻译热键被触发");
                OnTranslationHotkeyTriggered();
                return;
            }

            // 检查 OCR 快捷键
            if (IsHotkeyMatch(e, _ocrHotkey))
            {
                _logger.ZLogInformation($"OCR热键被触发");
                OnOcrHotkeyTriggered();
                return;
            }
        }

        /// <summary>
        /// 检查快捷键是否匹配
        /// </summary>
        private bool IsHotkeyMatch(KeyboardHookEventArgs e, HotkeyConfig config)
        {
            return (e.RawEvent.Mask & config.Modifiers) == config.Modifiers &&
                   e.Data.KeyCode == config.Key;
        }

        /// <summary>
        /// 翻译热键触发处理
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
            WeakReferenceMessenger.Default.Send(new TriggerTranslationMessage(text),"text");
        }

        /// <summary>
        /// OCR热键触发处理
        /// </summary>
        private void OnOcrHotkeyTriggered()
        {
            _logger.ZLogInformation($"OCR热键被触发，打开屏幕截图窗口");
            
            // 发送消息打开屏幕捕获窗口
            WeakReferenceMessenger.Default.Send(new TriggerTranslationMessage(string.Empty),"ocr");
        }
        
        public void Dispose()
        {
            if (_globalHook != null)
            {
                _globalHook.KeyPressed -= OnGlobalKeyPressed;
                if (_globalHook.IsRunning)
                {
                    _globalHook.Stop();
                }
                _globalHook.Dispose();
                _logger.ZLogInformation($"翻译热键服务已释放");
            }
        }
        
        public async ValueTask DisposeAsync()
        {
            if (_globalHook != null)
            {
                _globalHook.KeyPressed -= OnGlobalKeyPressed;
                if (_globalHook.IsRunning)
                {
                    _globalHook.Stop();
                }
                _globalHook.Dispose();
                _logger.ZLogInformation($"翻译热键服务已释放");
            }
            await Task.CompletedTask;
        }
    }
}