using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WordLens.Messages;
using ZLogger;

namespace WordLens.Services
{
    public interface IHotkeyManagerService
    {
        Task StartAsync();
    }


    public class HotkeyManagerService : IHotkeyManagerService
    {
        private readonly IHotkeyService _hotkeyService;
        private readonly IOcrHotkeyService _ocrHotkeyService;
        private readonly ISelectionService _selectionService;
        private readonly ILogger<HotkeyManagerService> _logger;

        public HotkeyManagerService(
            IHotkeyService hotkeyService,
            IOcrHotkeyService ocrHotkeyService,
            ISelectionService selectionService,
            ILogger<HotkeyManagerService> logger)
        {
            _hotkeyService = hotkeyService;
            _ocrHotkeyService = ocrHotkeyService;
            _selectionService = selectionService;
            _logger = logger;
        }

        public async Task StartAsync()
        {
            _logger.ZLogInformation($"热键管理服务启动");
            
            // 订阅事件
            _hotkeyService.HotkeyTriggered += OnTranslationHotkeyTriggered;
            _ocrHotkeyService.OcrHotkeyTriggered += OnOcrHotkeyTriggered;

            // 并行启动两个热键服务
            await Task.WhenAll(
                _hotkeyService.StartAsync(),
                _ocrHotkeyService.StartAsync()
            );
            
            _logger.ZLogInformation($"热键管理服务启动完成");
        }

        private void OnTranslationHotkeyTriggered(object? sender, EventArgs e)
        {
            _logger.ZLogInformation($"翻译热键被触发");
            
            var text = _selectionService.GetSelectedTex();
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.ZLogWarning($"未选择文本，忽略翻译热键");
                return;
            }

            _logger.ZLogInformation($"获取到选中文本，长度: {text.Length}");
            WeakReferenceMessenger.Default.Send(new ShowPopupMessage(text));
        }

        private void OnOcrHotkeyTriggered(object? sender, EventArgs e)
        {
            _logger.ZLogInformation($"OCR热键被触发（功能预留）");
            // TODO: 未来在这里实现OCR功能
            // 1. 捕获屏幕截图
            // 2. 调用OCR引擎识别文字
            // 3. 发送识别结果到翻译窗口
        }
    }
}