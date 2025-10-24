using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using HPPH;
using Microsoft.Extensions.Logging;
using ScreenCapture.NET;
using WordLens.Messages;
using WordLens.Services;
using ZLogger;

namespace WordLens.ViewModels
{
    /// <summary>
    /// 屏幕捕获窗口的ViewModel
    /// 管理区域选择、截图和OCR流程
    /// </summary>
    public partial class ScreenCaptureViewModel : ViewModelBase
    {
        private readonly IScreenshotService _screenshotService;
        private readonly ILogger<ScreenCaptureViewModel> _logger;

        [ObservableProperty]
        private Point startPoint;

        [ObservableProperty]
        private Point endPoint;

        [ObservableProperty]
        private bool isSelecting;

        [ObservableProperty]
        private Rect selectionRect;

        [ObservableProperty]
        private string sizeHint = "";

        /// <summary>
        /// 用于保存截图的临时目录
        /// </summary>
        private readonly string _tempScreenshotDir;

        public ScreenCaptureViewModel()
        {
            // 设计时构造函数
            _screenshotService = null!;
            _logger = null!;
            _tempScreenshotDir = string.Empty;
        }

        public ScreenCaptureViewModel(
            IScreenshotService screenshotService,
            ILogger<ScreenCaptureViewModel> logger)
        {
            _screenshotService = screenshotService;
            _logger = logger;

            // 创建临时截图目录
            _tempScreenshotDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WordLens",
                "Screenshots"
            );
            Directory.CreateDirectory(_tempScreenshotDir);

            _logger.ZLogInformation($"屏幕捕获ViewModel初始化完成");
        }

        /// <summary>
        /// 开始选择区域
        /// </summary>
        public void BeginSelection(Point point)
        {
            StartPoint = point;
            EndPoint = point;
            IsSelecting = true;
            UpdateSelectionRect();
            _logger.ZLogDebug($"开始选择区域: {point}");
        }

        /// <summary>
        /// 更新选择区域
        /// </summary>
        public void UpdateSelection(Point point)
        {
            if (!IsSelecting) return;

            EndPoint = point;
            UpdateSelectionRect();
        }

        /// <summary>
        /// 完成选择并执行截图
        /// </summary>
        public async Task<bool> CompleteSelectionAsync(Point point)
        {
            if (!IsSelecting) return false;

            EndPoint = point;
            IsSelecting = false;
            UpdateSelectionRect();

            // 检查选区大小
            if (SelectionRect.Width < 10 || SelectionRect.Height < 10)
            {
                _logger.ZLogWarning($"选区过小，取消截图: {SelectionRect}");
                return false;
            }

            _logger.ZLogInformation($"完成区域选择: {SelectionRect}");

            // 执行截图
            await CaptureAndProcessAsync();
            return true;
        }

        /// <summary>
        /// 取消选择
        /// </summary>
        public void CancelSelection()
        {
            IsSelecting = false;
            SelectionRect = new Rect();
            _logger.ZLogInformation($"取消区域选择");
        }

        /// <summary>
        /// 更新选择矩形
        /// </summary>
        private void UpdateSelectionRect()
        {
            double x = Math.Min(StartPoint.X, EndPoint.X);
            double y = Math.Min(StartPoint.Y, EndPoint.Y);
            double width = Math.Abs(EndPoint.X - StartPoint.X);
            double height = Math.Abs(EndPoint.Y - StartPoint.Y);

            SelectionRect = new Rect(x, y, width, height);

            // 更新尺寸提示
            SizeHint = $"{(int)width} × {(int)height}";
        }

        /// <summary>
        /// 执行截图并处理
        /// </summary>
        private async Task CaptureAndProcessAsync()
        {
            try
            {
                _logger.ZLogInformation($"开始截图: {SelectionRect}");

                
                
                // 执行截图
                var bitmap = await _screenshotService.CaptureAreaAsync(SelectionRect);

                if (bitmap == null)
                {
                    _logger.ZLogError($"截图失败，返回null");
                    return;
                }

                _logger.ZLogInformation($"截图成功: {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");

                // 保存截图到临时文件
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = $"screenshot_{timestamp}.png";
                string filepath = Path.Combine(_tempScreenshotDir, filename);

                SaveBitmap(bitmap, filepath);
                _logger.ZLogInformation($"截图已保存: {filepath}");

                // TODO: 这里将来可以调用OCR服务识别文字
                // var text = await _ocrService.RecognizeTextAsync(bitmap);
                // if (!string.IsNullOrWhiteSpace(text))
                // {
                //     WeakReferenceMessenger.Default.Send(new ShowPopupMessage(text));
                // }

                // 暂时：发送消息显示截图成功
                _logger.ZLogInformation($"截图完成，等待OCR功能实现");
            }
            catch (Exception ex)
            {
                _logger.ZLogError(ex, $"截图或保存过程中发生错误");
            }
        }

        /// <summary>
        /// 保存位图到文件
        /// </summary>
        private void SaveBitmap(WriteableBitmap bitmap, string filepath)
        {
            try
            {
                using var fileStream = File.Create(filepath);
                bitmap.Save(fileStream);
                _logger.ZLogInformation($"位图已保存到: {filepath}");
            }
            catch (Exception ex)
            {
                _logger.ZLogError(ex, $"保存位图失败: {filepath}");
                throw;
            }
        }

        /// <summary>
        /// 获取虚拟屏幕边界（用于多显示器）
        /// </summary>
        public Rect GetVirtualScreenBounds()
        {
            return _screenshotService.GetVirtualScreenBounds();
        }
    }
}