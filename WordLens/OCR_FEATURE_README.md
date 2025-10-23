# OCR截图功能使用说明

## 功能概述

WordLens现在支持OCR屏幕截图功能！用户可以通过快捷键触发全屏截图，选择任意区域进行截图。

## 当前实现状态

### ✅ 已完成功能

1. **跨平台架构设计**
   - 抽象的截图服务接口（`IScreenshotService`）
   - Windows平台完整实现
   - Linux/macOS平台接口预留

2. **屏幕捕获窗口**
   - 全屏透明遮罩
   - 鼠标拖拽选择区域
   - 实时显示选区尺寸
   - ESC键取消功能

3. **截图功能**
   - 支持多显示器
   - DPI缩放适配
   - 截图自动保存到临时目录

4. **集成流程**
   - OCR快捷键触发（默认：Ctrl+Shift+O）
   - 与现有热键系统无缝集成

### ⏳ 待实现功能

1. **OCR文字识别**
   - OCR服务接口已预留（`IOcrService`）
   - 可选实现方案：
     - Windows.Media.Ocr（推荐）
     - Tesseract
     - 在线OCR服务

2. **平台扩展**
   - Linux截图实现（X11/Wayland）
   - macOS截图实现（CGImage）

## 使用方法

### 基本使用

1. **打开应用程序**
2. **按下OCR快捷键**（默认：Ctrl+Shift+O）
3. **屏幕变暗**，显示全屏遮罩
4. **拖拽鼠标**选择要截图的区域
5. **释放鼠标**完成截图
6. **按ESC**取消截图

### 自定义快捷键

在设置界面可以修改OCR快捷键：
1. 打开主窗口设置
2. 找到"OCR快捷键"设置项
3. 点击"设置OCR快捷键"按钮
4. 按下想要使用的快捷键组合

### 截图保存位置

截图自动保存在：
```
Windows: %APPDATA%\WordLens\Screenshots\
Linux:   ~/.config/WordLens/Screenshots/
macOS:   ~/Library/Application Support/WordLens/Screenshots/
```

文件命名格式：`screenshot_yyyyMMdd_HHmmss.png`

## 技术架构

### 核心组件

```
┌─────────────────────────────────────────┐
│     用户按下OCR快捷键                    │
└────────────┬────────────────────────────┘
             ↓
┌─────────────────────────────────────────┐
│   HotkeyManagerService                   │
│   └─ OnOcrHotkeyTriggered()             │
└────────────┬────────────────────────────┘
             ↓
┌─────────────────────────────────────────┐
│   发送ShowOcrCaptureMessage              │
└────────────┬────────────────────────────┘
             ↓
┌─────────────────────────────────────────┐
│   ApplicationViewModel                   │
│   └─ 创建ScreenCaptureWindow            │
└────────────┬────────────────────────────┘
             ↓
┌─────────────────────────────────────────┐
│   ScreenCaptureWindow (全屏遮罩)         │
│   └─ 用户拖拽选择区域                    │
└────────────┬────────────────────────────┘
             ↓
┌─────────────────────────────────────────┐
│   ScreenCaptureViewModel                 │
│   └─ CompleteSelectionAsync()           │
└────────────┬────────────────────────────┘
             ↓
┌─────────────────────────────────────────┐
│   IScreenshotService                     │
│   └─ CaptureAreaAsync(rect)             │
└────────────┬────────────────────────────┘
             ↓
┌─────────────────────────────────────────┐
│   保存截图 → [预留] OCR识别 → 显示翻译   │
└─────────────────────────────────────────┘
```

### 文件结构

```
WordLens/
├── Services/
│   ├── IScreenshotService.cs              # 截图服务接口
│   ├── WindowsScreenshotService.cs        # Windows实现
│   ├── LinuxScreenshotService.cs          # Linux实现(预留)
│   ├── MacScreenshotService.cs            # macOS实现(预留)
│   └── IOcrService.cs                     # OCR接口(预留)
├── ViewModels/
│   ├── ScreenCaptureViewModel.cs          # 截图窗口VM
│   └── ApplicationViewModel.cs            # [已修改]注册消息
├── Views/
│   ├── ScreenCaptureWindow.axaml          # 截图窗口UI
│   └── ScreenCaptureWindow.axaml.cs       # 截图窗口逻辑
├── Messages/
│   └── ShowOcrCaptureMessage.cs           # OCR触发消息
└── App.axaml.cs                           # [已修改]注册服务
```

## 开发指南

### 添加OCR功能

要完成OCR识别功能，需要：

1. **实现IOcrService接口**
   ```csharp
   public class WindowsOcrService : IOcrService
   {
       public async Task<string?> RecognizeTextAsync(
           WriteableBitmap bitmap, 
           string languageCode = "zh-CN")
       {
           // 使用Windows.Media.Ocr实现
       }
   }
   ```

2. **在ScreenCaptureViewModel中集成**
   ```csharp
   private async Task CaptureAndProcessAsync()
   {
       var bitmap = await _screenshotService.CaptureAreaAsync(SelectionRect);
       
       // 添加OCR识别
       var text = await _ocrService.RecognizeTextAsync(bitmap);
       
       // 发送到翻译窗口
       if (!string.IsNullOrWhiteSpace(text))
       {
           WeakReferenceMessenger.Default.Send(new ShowPopupMessage(text));
       }
   }
   ```

3. **在App.axaml.cs中注册服务**
   ```csharp
   if (OperatingSystem.IsWindows())
   {
       services.AddSingleton<IOcrService, WindowsOcrService>();
   }
   ```

### 扩展到其他平台

#### Linux实现示例

```csharp
// LinuxScreenshotService.cs
public async Task<WriteableBitmap?> CaptureAreaAsync(Rect area)
{
    // 方案1: 使用X11 API
    // 方案2: 调用系统命令 (scrot, gnome-screenshot)
    // 方案3: 使用Wayland协议
}
```

#### macOS实现示例

```csharp
// MacScreenshotService.cs
public async Task<WriteableBitmap?> CaptureAreaAsync(Rect area)
{
    // 使用CGWindowListCreateImage或CGDisplayCreateImage
}
```

## 已知问题和限制

1. **Linux和macOS暂未实现**
   - 仅Windows平台可用
   - 其他平台需要额外实现

2. **OCR识别未实现**
   - 截图功能完整
   - 需要手动添加OCR引擎

3. **高DPI支持**
   - 已处理DPI缩放
   - 某些极端情况可能需要调整

## 性能优化建议

1. **延迟加载**：首次使用时初始化OCR引擎
2. **图片预处理**：二值化、降噪提高识别率
3. **缓存机制**：避免重复识别相同区域
4. **异步处理**：所有耗时操作使用异步

## 贡献指南

如果你想为此功能贡献代码：

1. **实现Linux/macOS截图**
2. **添加OCR引擎集成**
3. **优化用户体验**（如选区辅助线、放大镜等）
4. **添加测试用例**

## 许可证

此功能遵循WordLens项目的许可证。