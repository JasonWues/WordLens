# WordLens - 智能划词翻译工具

WordLens 是一个基于 Avalonia UI 开发的跨平台划词翻译工具，支持多翻译源并行请求、OCR识别（预留）以及详细的日志记录功能。

## ✨ 主要特性

### 🚀 核心功能
- **快捷键翻译**：通过自定义快捷键快速翻译选中的文本
- **多翻译源支持**：可配置多个翻译API，并行请求获得更全面的翻译结果
- **OCR热键**：预留OCR功能接口，支持未来扩展图像文字识别
- **智能翻译窗口**：美观的翻译结果展示，支持垂直列表显示多个翻译源结果

### 🛠️ 高级特性
- **翻译源管理**：
  - 添加、删除、排序翻译源
  - 独立启用/禁用每个翻译源
  - 自定义API配置（BaseURL、API Key、Model）
- **代理支持**：HTTP代理配置，支持带认证的代理
- **结构化日志**：使用 ZLogger 记录所有关键操作和异常
- **MVVM架构**：清晰的代码结构，易于维护和扩展

## 📦 安装要求

- .NET 10.0 或更高版本
- Windows 11 / macOS / Linux

## 🚀 快速开始

### 1. 克隆项目

```bash
git clone <repository-url>
cd WordLens
```

### 2. 构建项目

```bash
# 构建项目（包含 Rust 原生库）
dotnet build

# 或发布为独立应用
dotnet publish -c Release
```

### 3. 运行

```bash
dotnet run --project WordLens
```

## 📖 使用指南

### 基本使用

1. **启动应用**：首次启动时会在系统托盘显示图标
2. **设置快捷键**：
   - 右键托盘图标 → "设置"
   - 在"常规"标签页设置翻译快捷键（默认：Ctrl+Shift+T）
   - 设置OCR快捷键（默认：Ctrl+Shift+W，功能预留）
3. **配置翻译源**：
   - 切换到"翻译源"标签页
   - 添加或编辑翻译源配置
   - 使用复选框启用/禁用特定翻译源
4. **开始翻译**：
   - 在任何应用中选中文本
   - 按下设置的快捷键
   - 翻译窗口会自动弹出并显示所有已启用翻译源的结果

### 翻译源配置

#### 支持的翻译源类型

目前支持 **OpenAI 兼容接口**，可配置：

- **OpenAI**：官方 API
- **其他兼容服务**：任何提供 OpenAI 兼容接口的服务

#### 配置示例

```json
{
  "Name": "OpenAI",
  "Type": "OpenAI",
  "BaseUrl": "https://api.openai.com",
  "ApiKey": "your-api-key-here",
  "Model": "gpt-4o-mini",
  "IsEnabled": true
}
```

#### 添加新翻译源

1. 点击"翻译源"标签页
2. 点击"添加翻译源"按钮
3. 填写配置信息：
   - **名称**：翻译源的显示名称
   - **Base URL**：API 基础地址
   - **API Key**：认证密钥
   - **Model**：使用的模型名称
4. 勾选"启用此翻译源"
5. 点击"应用"保存设置

### 多翻译源并行请求

WordLens 支持同时使用多个翻译源：

1. 配置多个翻译源并全部启用
2. 触发翻译时，所有启用的翻译源会**并行请求**
3. 翻译窗口会实时显示每个翻译源的结果
4. 每个翻译源独立显示：
   - ✓ 成功：显示翻译结果
   - ✗ 失败：显示错误信息

**优势**：
- ⚡ 更快的响应速度（并行而非串行）
- 📊 对比不同翻译引擎的结果
- 🛡️ 某个翻译源失败不影响其他源

### 网络代理配置

如果需要通过代理访问翻译API：

1. 切换到"网络代理"标签页
2. 勾选"启用 HTTP 代理"
3. 配置代理信息：
   - 代理地址：如 `http://127.0.0.1`
   - 端口：如 `8080`
   - 如需认证，勾选"需要身份验证"并填写用户名密码
4. 点击"应用"保存

### 翻译窗口功能

翻译窗口提供以下功能：

- **置顶窗口**：点击按钮可切换窗口置顶状态
- **复制原文**：快速复制选中的原始文本
- **复制翻译**：每个翻译结果都有独立的复制按钮
- **清空内容**：清除当前显示的内容
- **滚动查看**：支持滚动查看多个翻译结果

## 📁 配置文件

配置文件位置：`%APPDATA%/WordLens/settings.json` (Windows)

示例配置：

```json
{
  "Hotkey": {
    "Modifiers": "LeftCtrl, LeftShift",
    "Key": "VcT"
  },
  "OcrHotkey": {
    "Modifiers": "LeftCtrl, LeftShift",
    "Key": "VcW"
  },
  "TargetLanguage": "zh-CN",
  "SelectedProvider": "OpenAI",
  "Providers": [
    {
      "Name": "OpenAI",
      "Type": "OpenAI",
      "BaseUrl": "https://api.openai.com",
      "ApiKey": "sk-...",
      "Model": "gpt-4o-mini",
      "IsEnabled": true
    },
    {
      "Name": "DeepSeek",
      "Type": "OpenAI",
      "BaseUrl": "https://api.deepseek.com",
      "ApiKey": "sk-...",
      "Model": "deepseek-chat",
      "IsEnabled": true
    }
  ],
  "Proxy": {
    "Enabled": false,
    "Address": "http://127.0.0.1",
    "Port": 8080,
    "UseAuthentication": false,
    "Username": null,
    "Password": null
  }
}
```

## 📝 日志系统

WordLens 使用 ZLogger 记录详细的运行日志。

### 日志位置

- Windows: `%APPDATA%/WordLens/logs/`
- macOS: `~/Library/Application Support/WordLens/logs/`
- Linux: `~/.config/WordLens/logs/`

### 日志文件命名

- 格式：`wordlens-yyyy-MM-dd_index.log`
- 示例：`wordlens-2025-01-20_0.log`

### 日志级别

- **Information**：正常操作日志
- **Warning**：警告信息（如无启用的翻译源）
- **Error**：错误信息（如API请求失败）

### 日志保留策略

- 按日期自动轮转
- 单个文件最大 10MB
- 默认保留 30 天

### 查看日志

日志记录以下关键事件：

```
[Info] 设置服务初始化，配置文件路径: C:\Users\...\settings.json
[Info] 翻译热键服务启动，快捷键配置: Modifiers=LeftCtrl, LeftShift, Key=VcT
[Info] OCR热键服务启动，快捷键配置: Modifiers=LeftCtrl, LeftShift, Key=VcW
[Info] 热键管理服务启动
[Info] 翻译热键被触发
[Info] 获取到选中文本，长度: 25
[Info] 开始翻译，文本长度: 25，启用的翻译源数量: 2
[Info] 开始使用 OpenAI 翻译
[Info] OpenAI 翻译成功，结果长度: 23
[Info] 开始使用 DeepSeek 翻译
[Info] DeepSeek 翻译成功，结果长度: 25
[Info] 翻译完成，成功: 2/2
```

## 🔧 技术架构

### 技术栈

- **UI框架**：Avalonia UI 11.3.7
- **MVVM框架**：CommunityToolkit.Mvvm 8.4.0
- **日志框架**：ZLogger 2.5.10
- **热键管理**：SharpHook 7.0.3
- **HTTP客户端**：Microsoft.Extensions.Http
- **原生模块**：Rust (用于文本选择)

### 项目结构

```
WordLens/
├── Models/              # 数据模型
│   ├── AppSettings.cs
│   └── TranslationResult.cs
├── Services/            # 服务层
│   ├── HotkeyService.cs
│   ├── OcrHotkeyService.cs
│   ├── HotkeyManagerService.cs
│   ├── TranslationServices.cs
│   ├── SettingsService.cs
│   └── SelectionService.cs
├── ViewModels/          # 视图模型
│   ├── ApplicationViewModel.cs
│   ├── MainWindowViewModel.cs
│   ├── SettingsViewModel.cs
│   └── PopupWindowViewModel.cs
├── Views/               # 视图
│   ├── MainWindowView.axaml
│   └── PopupWindowView.axaml
├── Messages/            # 消息传递
└── Util/                # 工具类

native/                  # Rust 原生模块
├── src/
│   └── lib.rs
└── Cargo.toml
```

### 核心流程

1. **热键触发** → `HotkeyService` 检测快捷键
2. **文本获取** → `SelectionService` 获取选中文本
3. **消息发送** → `ShowPopupMessage` 通知应用
4. **并行翻译** → `TranslationService` 并行请求所有已启用翻译源
5. **结果显示** → `PopupWindowView` 显示多个翻译结果

## 🔮 未来计划

### 即将推出
- [ ] OCR 功能实现（Tesseract/PaddleOCR）
- [ ] 更多翻译源支持（Google、DeepL、百度）
- [ ] 翻译历史记录
- [ ] 自定义翻译规则
- [ ] 主题定制

### 长期规划
- [ ] 浏览器扩展
- [ ] 移动端支持
- [ ] 离线翻译
- [ ] AI 辅助翻译优化

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

### 开发指南

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

## 📄 许可证

本项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件

## 🙏 致谢

- [Avalonia UI](https://avaloniaui.net/) - 跨平台UI框架
- [SharpHook](https://github.com/TolikPylypchuk/SharpHook) - 全局热键管理
- [ZLogger](https://github.com/Cysharp/ZLogger) - 高性能日志框架

## 📧 联系方式

如有问题或建议，欢迎通过以下方式联系：

- 提交 Issue
- 发送邮件
- 加入讨论组

---

**WordLens** - 让翻译更简单、更智能 🌍✨