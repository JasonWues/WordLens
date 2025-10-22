# WordLens 功能增强技术设计文档

## 1. 概述

本文档描述了为 WordLens 划词翻译软件添加以下功能的技术设计：
- OCR 热键设置（预留功能）
- 多翻译源并行请求和显示
- 结构化日志记录

## 2. 系统架构

### 2.1 架构图

```
┌─────────────────────────────────────────────────────────┐
│                      User Interface                      │
├─────────────────────────────────────────────────────────┤
│  MainWindowView          │     PopupWindowView          │
│  (设置界面)              │     (翻译结果窗口)           │
│  - 快捷键设置            │     - 原文显示                │
│  - 翻译源管理            │     - 多翻译结果列表          │
│  - 代理设置              │     - 加载状态                │
└─────────────────────────────────────────────────────────┘
                          ↕
┌─────────────────────────────────────────────────────────┐
│                      View Models                         │
├─────────────────────────────────────────────────────────┤
│  SettingsViewModel       │  PopupWindowViewModel        │
│  - 快捷键捕获            │  - 翻译结果集合               │
│  - 翻译源配置            │  - 翻译命令                   │
└─────────────────────────────────────────────────────────┘
                          ↕
┌─────────────────────────────────────────────────────────┐
│                        Services                          │
├─────────────────────────────────────────────────────────┤
│  HotkeyManagerService                                    │
│  ├─ HotkeyService (翻译热键)                             │
│  └─ OcrHotkeyService (OCR热键-预留)                      │
│                                                          │
│  TranslationService                                      │
│  └─ 并行请求多个翻译源                                    │
│                                                          │
│  SettingsService                                         │
│  └─ 配置持久化                                           │
│                                                          │
│  Logger (ZLogger)                                        │
│  └─ 文件日志输出                                         │
└─────────────────────────────────────────────────────────┘
```

### 2.2 核心组件说明

#### 2.2.1 热键管理
- **HotkeyService**: 处理翻译快捷键
- **OcrHotkeyService**: 处理OCR快捷键（预留）
- **HotkeyManagerService**: 统一管理和分发热键事件

#### 2.2.2 翻译服务
- **TranslationService**: 协调多个翻译源
- **ITranslationProvider**: 翻译提供商接口
- **OpenAITranslationProvider**: OpenAI兼容接口实现

#### 2.2.3 数据持久化
- **SettingsService**: 配置的加载和保存
- **AppSettings**: 应用配置数据模型

## 3. 数据模型设计

### 3.1 AppSettings 增强

```csharp
public class AppSettings
{
    // 现有字段
    public HotkeyConfig Hotkey { get; set; }
    public HotkeyConfig OcrHotkey { get; set; }  // 已存在
    public string TargetLanguage { get; set; }
    public string? SelectedProvider { get; set; }
    public ProxyConfig Proxy { get; set; }
    public List<ProviderConfig> Providers { get; set; }
}
```

### 3.2 ProviderConfig 增强

```csharp
public class ProviderConfig
{
    public string Name { get; set; }
    public ProviderType Type { get; set; }
    public string BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string Model { get; set; }
    
    // 新增：启用/禁用标志
    public bool IsEnabled { get; set; } = true;
}
```

### 3.3 新增 TranslationResult 模型

```csharp
public class TranslationResult
{
    /// <summary>
    /// 翻译源名称
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;
    
    /// <summary>
    /// 翻译结果文本
    /// </summary>
    public string? Result { get; set; }
    
    /// <summary>
    /// 是否翻译成功
    /// </summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 是否正在加载
    /// </summary>
    public bool IsLoading { get; set; }
}
```

## 4. 服务层设计

### 4.1 OcrHotkeyService

```csharp
public interface IOcrHotkeyService : IAsyncDisposable
{
    event EventHandler? OcrHotkeyTriggered;
    Task StartAsync(CancellationToken ct = default);
    void Stop();
}

public class OcrHotkeyService : IOcrHotkeyService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<OcrHotkeyService> _logger;
    private HotkeyConfig _config;
    private EventLoopGlobalHook? _hook;
    
    // 实现与 HotkeyService 类似
}
```

### 4.2 HotkeyManagerService 增强

```csharp
public class HotkeyManagerService : IHotkeyManagerService
{
    private readonly IHotkeyService _hotkeyService;
    private readonly IOcrHotkeyService _ocrHotkeyService;
    private readonly ISelectionService _selectionService;
    private readonly ILogger<HotkeyManagerService> _logger;
    
    public async Task StartAsync()
    {
        _hotkeyService.HotkeyTriggered += OnTranslationHotkeyTriggered;
        _ocrHotkeyService.OcrHotkeyTriggered += OnOcrHotkeyTriggered;
        
        await Task.WhenAll(
            _hotkeyService.StartAsync(),
            _ocrHotkeyService.StartAsync()
        );
    }
    
    private void OnTranslationHotkeyTriggered(object? sender, EventArgs e)
    {
        _logger.ZLogInformation("翻译热键被触发");
        // 现有逻辑
    }
    
    private void OnOcrHotkeyTriggered(object? sender, EventArgs e)
    {
        _logger.ZLogInformation("OCR热键被触发（功能预留）");
        // TODO: 未来实现OCR功能
    }
}
```

### 4.3 TranslationService 重构

```csharp
public class TranslationService
{
    private readonly ISettingsService _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TranslationService> _logger;
    
    /// <summary>
    /// 并行翻译文本，使用所有启用的翻译源
    /// </summary>
    public async Task<List<TranslationResult>> TranslateAsync(
        string text, 
        CancellationToken ct = default)
    {
        var cfg = await _settings.LoadAsync();
        var enabledProviders = cfg.Providers.Where(p => p.IsEnabled).ToList();
        
        _logger.ZLogInformation(
            $"开始翻译，文本长度: {text.Length}，启用的翻译源数量: {enabledProviders.Count}"
        );
        
        if (enabledProviders.Count == 0)
        {
            _logger.ZLogWarning("没有启用的翻译源");
            return new List<TranslationResult>();
        }
        
        // 为每个翻译源创建任务
        var tasks = enabledProviders.Select(provider => 
            TranslateSingleProviderAsync(provider, text, cfg, ct)
        ).ToList();
        
        // 并行执行所有翻译任务
        var results = await Task.WhenAll(tasks);
        
        var successCount = results.Count(r => r.IsSuccess);
        _logger.ZLogInformation(
            $"翻译完成，成功: {successCount}/{results.Length}"
        );
        
        return results.ToList();
    }
    
    /// <summary>
    /// 单个翻译源的翻译任务
    /// </summary>
    private async Task<TranslationResult> TranslateSingleProviderAsync(
        ProviderConfig providerCfg,
        string text,
        AppSettings settings,
        CancellationToken ct)
    {
        var result = new TranslationResult 
        { 
            ProviderName = providerCfg.Name,
            IsLoading = true
        };
        
        try
        {
            _logger.ZLogInformation(
                $"开始使用 {providerCfg.Name} 翻译"
            );
            
            ITranslationProvider provider = providerCfg.Type switch
            {
                ProviderType.OpenAI => new OpenAITranslationProvider(providerCfg),
                _ => throw new NotSupportedException(
                    $"不支持的翻译源类型: {providerCfg.Type}"
                )
            };
            
            var httpClient = CreateHttpClientWithProxy(settings.Proxy);
            result.Result = await provider.TranslateAsync(
                text, 
                settings.TargetLanguage, 
                httpClient, 
                ct
            );
            result.IsSuccess = true;
            
            _logger.ZLogInformation(
                $"{providerCfg.Name} 翻译成功，结果长度: {result.Result?.Length ?? 0}"
            );
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            
            _logger.ZLogError(ex, 
                $"{providerCfg.Name} 翻译失败: {ex.Message}"
            );
        }
        finally
        {
            result.IsLoading = false;
        }
        
        return result;
    }
}
```

## 5. UI 设计

### 5.1 PopupWindowViewModel 增强

```csharp
public partial class PopupWindowViewModel : ViewModelBase
{
    private readonly TranslationService _translationService;
    private readonly ILogger<PopupWindowViewModel> _logger;
    
    [ObservableProperty]
    private bool isBusy;
    
    [ObservableProperty]
    private string? sourceText;
    
    // 改为翻译结果集合
    [ObservableProperty]
    private ObservableCollection<TranslationResult> translationResults = new();
    
    [ObservableProperty]
    private bool isTopmost;
    
    [RelayCommand]
    public async Task TranslateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SourceText))
            return;
        
        IsBusy = true;
        TranslationResults.Clear();
        
        try
        {
            _logger.ZLogInformation("开始翻译请求");
            
            var results = await _translationService.TranslateAsync(
                SourceText, 
                cancellationToken
            );
            
            foreach (var result in results)
            {
                TranslationResults.Add(result);
            }
            
            _logger.ZLogInformation(
                $"翻译结果已添加到UI，共 {results.Count} 个"
            );
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, "翻译过程中发生异常");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

### 5.2 PopupWindowView.axaml 重新设计

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:WordLens.ViewModels"
        x:Class="WordLens.Views.PopupWindowView"
        x:DataType="vm:PopupWindowViewModel"
        Width="600" Height="500"
        CanResize="True"
        Topmost="{Binding IsTopmost}"
        Title="WordLens">

    <Border Padding="16">
        <Grid RowDefinitions="Auto,12,Auto,*,12,Auto,*">
            
            <!-- 工具栏 -->
            <Border Grid.Row="0" 
                    Background="#F5F5F5" 
                    CornerRadius="4" 
                    Padding="8,4">
                <Grid ColumnDefinitions="*,Auto">
                    <Button Classes="icon-button"
                            Command="{Binding ToggleTopmostCommand}">
                        置顶
                    </Button>
                </Grid>
            </Border>
            
            <!-- 原文标题 -->
            <TextBlock Grid.Row="2" 
                       Text="原文" 
                       FontWeight="Bold" />
            
            <!-- 原文内容 -->
            <Border Grid.Row="3"
                    Background="#F9F9F9"
                    BorderBrush="#E0E0E0"
                    BorderThickness="1"
                    CornerRadius="4"
                    Padding="12">
                <ScrollViewer>
                    <TextBlock Text="{Binding SourceText}" 
                               TextWrapping="Wrap" />
                </ScrollViewer>
            </Border>
            
            <!-- 进度条 -->
            <ProgressBar Grid.Row="4" 
                         IsVisible="{Binding IsBusy}" 
                         IsIndeterminate="True"
                         Height="3" />
            
            <!-- 翻译结果标题 -->
            <TextBlock Grid.Row="5" 
                       Text="翻译结果" 
                       FontWeight="Bold" />
            
            <!-- 翻译结果列表（垂直显示） -->
            <ScrollViewer Grid.Row="6">
                <ItemsControl ItemsSource="{Binding TranslationResults}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Border Background="#F9F9F9"
                                    BorderBrush="#E0E0E0"
                                    BorderThickness="1"
                                    CornerRadius="4"
                                    Padding="12"
                                    Margin="0,0,0,12">
                                <Grid RowDefinitions="Auto,8,Auto">
                                    <!-- 翻译源名称 -->
                                    <Grid Grid.Row="0" 
                                          ColumnDefinitions="*,Auto">
                                        <TextBlock Grid.Column="0"
                                                   Text="{Binding ProviderName}"
                                                   FontWeight="SemiBold"
                                                   FontSize="14" />
                                        
                                        <!-- 状态指示器 -->
                                        <StackPanel Grid.Column="1" 
                                                    Orientation="Horizontal"
                                                    Spacing="4">
                                            <!-- 加载中 -->
                                            <ProgressRing Width="16" 
                                                         Height="16"
                                                         IsVisible="{Binding IsLoading}"
                                                         IsIndeterminate="True" />
                                            
                                            <!-- 成功图标 -->
                                            <PathIcon Width="16" 
                                                     Height="16"
                                                     Data="{StaticResource checkmark_circle_regular}"
                                                     Foreground="Green"
                                                     IsVisible="{Binding IsSuccess}" />
                                            
                                            <!-- 失败图标 -->
                                            <PathIcon Width="16" 
                                                     Height="16"
                                                     Data="{StaticResource error_circle_regular}"
                                                     Foreground="Red"
                                                     IsVisible="{Binding !IsSuccess}" />
                                        </StackPanel>
                                    </Grid>
                                    
                                    <!-- 翻译结果或错误信息 -->
                                    <TextBlock Grid.Row="2"
                                               Text="{Binding Result}"
                                               IsVisible="{Binding IsSuccess}"
                                               TextWrapping="Wrap"
                                               LineHeight="22" />
                                    
                                    <TextBlock Grid.Row="2"
                                               Text="{Binding ErrorMessage}"
                                               IsVisible="{Binding !IsSuccess}"
                                               Foreground="Red"
                                               TextWrapping="Wrap"
                                               FontStyle="Italic" />
                                </Grid>
                            </Border>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </ScrollViewer>
        </Grid>
    </Border>
</Window>
```

### 5.3 SettingsViewModel 增强

为翻译源配置添加启用/禁用开关：

```csharp
// 在 SettingsView.axaml 的翻译源列表中添加
<CheckBox IsChecked="{Binding IsEnabled}"
          Content="启用"
          Margin="0,4,0,0" />
```

## 6. 日志策略

### 6.1 日志配置

在 `Program.cs` 或 `App.axaml.cs` 中配置 ZLogger：

```csharp
public class Program
{
    public static void Main(string[] args)
    {
        ConfigureLogging();
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }
    
    private static void ConfigureLogging()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WordLens",
            "logs"
        );
        Directory.CreateDirectory(logDir);
        
        var logFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            
            // 控制台输出（开发时）
            builder.AddZLoggerConsole();
            
            // 文件输出
            builder.AddZLoggerFile(
                Path.Combine(logDir, $"wordlens-{DateTime.Now:yyyyMMdd}.log"),
                options =>
                {
                    options.EnableStructuredLogging = true;
                }
            );
        });
        
        // 保存 LoggerFactory 供 DI 使用
        Services.ConfigureLogging(logFactory);
    }
}
```

### 6.2 日志级别规范

| 组件 | 操作 | 级别 | 示例 |
|------|------|------|------|
| HotkeyService | 热键触发 | Information | "翻译热键被触发" |
| HotkeyService | 热键重载 | Information | "热键配置已重新加载" |
| OcrHotkeyService | OCR热键触发 | Information | "OCR热键被触发（功能预留）" |
| TranslationService | 开始翻译 | Information | "开始翻译，文本长度: 50，启用的翻译源数量: 2" |
| TranslationService | 翻译成功 | Information | "OpenAI 翻译成功，结果长度: 45" |
| TranslationService | 翻译失败 | Warning | "DeepL 翻译失败: API密钥无效" |
| TranslationService | 翻译异常 | Error | "翻译过程中发生异常" |
| SettingsService | 加载配置 | Information | "配置已加载" |
| SettingsService | 保存配置 | Information | "配置已保存" |
| SettingsService | 配置错误 | Error | "配置文件损坏" |

### 6.3 日志文件管理

- **位置**: `%APPDATA%/WordLens/logs/`
- **命名**: `wordlens-yyyyMMdd.log`
- **保留**: 30天（可配置）
- **大小限制**: 单个文件最大 10MB（可配置）

## 7. 依赖注入配置

在 `App.axaml.cs` 中注册新服务：

```csharp
private void RegisterServices(ServiceCollection services)
{
    // 日志
    services.AddSingleton(loggerFactory);
    services.AddLogging();
    
    // 现有服务
    services.AddSingleton<ISettingsService, SettingsService>();
    services.AddSingleton<IHotkeyService, HotkeyService>();
    services.AddSingleton<ISelectionService, SelectionService>();
    services.AddHttpClient();
    
    // 新增服务
    services.AddSingleton<IOcrHotkeyService, OcrHotkeyService>();
    services.AddSingleton<IHotkeyManagerService, HotkeyManagerService>();
    services.AddSingleton<TranslationService>();
    
    // ViewModels
    services.AddTransient<MainWindowViewModel>();
    services.AddTransient<SettingsViewModel>();
    services.AddTransient<PopupWindowViewModel>();
    services.AddTransient<ApplicationViewModel>();
}
```

## 8. 实施步骤

### 阶段一：数据模型和基础服务（1-2天）

1. ✅ 更新 `ProviderConfig` 添加 `IsEnabled` 属性
2. ✅ 创建 `TranslationResult` 模型类
3. ✅ 创建 `OcrHotkeyService` 实现
4. ✅ 更新 `HotkeyManagerService` 集成 OCR 热键

### 阶段二：翻译服务重构（2-3天）

5. ✅ 重构 `TranslationService` 支持多翻译源
6. ✅ 实现并行翻译逻辑
7. ✅ 添加异常处理和错误信息

### 阶段三：UI 更新（2-3天）

8. ✅ 更新 `PopupWindowViewModel` 支持多结果
9. ✅ 重新设计 `PopupWindowView.axaml` 界面
10. ✅ 在 `SettingsViewModel` 中添加启用/禁用开关
11. ✅ 完善 OCR 热键捕获 UI

### 阶段四：日志集成（1-2天）

12. ✅ 配置 ZLogger
13. ✅ 在所有服务中添加日志记录
14. ✅ 测试日志输出

### 阶段五：测试和优化（2-3天）

15. ✅ 单元测试
16. ✅ 集成测试
17. ✅ 性能优化
18. ✅ 文档完善

**总预计时间：8-13 天**

## 9. 测试计划

### 9.1 单元测试

- `TranslationService` 多翻译源并行测试
- `OcrHotkeyService` 热键捕获测试
- 各服务日志输出测试

### 9.2 集成测试

- 完整翻译流程测试
- 多翻译源同时成功/失败场景
- 配置保存和加载测试

### 9.3 UI 测试

- 多翻译结果显示测试
- 热键捕获 UI 测试
- 翻译源启用/禁用测试

## 10. 风险和注意事项

### 10.1 性能考虑

- **并行请求数量限制**: 建议最多同时请求 5 个翻译源
- **超时设置**: 每个翻译源设置合理的超时时间（如 30 秒）
- **取消令牌**: 支持取消翻译请求

### 10.2 用户体验

- **加载状态**: 每个翻译源显示独立的加载状态
- **错误提示**: 友好的错误信息显示
- **响应速度**: 优先显示最快返回的结果

### 10.3 兼容性

- **配置向后兼容**: 确保旧配置文件能够正常迁移
- **默认值**: 为新增字段提供合理的默认值

## 11. 未来扩展

### 11.1 OCR 功能实现

当需要实现 OCR 功能时，可以考虑：
- **Tesseract**: 开源 OCR 引擎
- **PaddleOCR**: 高精度中文 OCR
- **在线 OCR API**: 百度 OCR、腾讯 OCR 等

### 11.2 更多翻译源

- Google 翻译 API
- DeepL API
- 百度翻译 API
- 有道翻译 API

### 11.3 高级功能

- 翻译历史记录
- 收藏夹功能
- 自定义翻译引擎
- 批量翻译

## 12. 参考资源

- [Avalonia UI 文档](https://docs.avaloniaui.net/)
- [ZLogger 文档](https://github.com/Cysharp/ZLogger)
- [SharpHook 文档](https://github.com/TolikPylypchuk/SharpHook)
- [MVVM 模式最佳实践](https://learn.microsoft.com/en-us/dotnet/architecture/maui/mvvm)