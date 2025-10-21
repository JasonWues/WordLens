# WordLens 设置界面使用指南

## 功能概述

WordLens 设置界面提供了完整的配置管理功能，包括：
- ✅ 多翻译源管理
- ✅ 快捷键自定义设置
- ✅ HTTP 代理配置
- ✅ 目标语言选择

## 使用说明

### 1. 打开设置窗口

通过系统托盘图标或快捷键打开设置窗口。

### 2. 常规设置

**目标语言**
- 从下拉菜单选择翻译的目标语言
- 支持中文、英文、日文、韩文、法语、德语、西班牙语、俄语等

**快捷键设置**
- 点击"设置快捷键"按钮
- 按下想要设置的快捷键组合（如 Ctrl+Shift+T）
- 系统会自动捕获并保存
- 按 ESC 取消设置

### 3. 翻译源管理

**添加翻译源**
1. 点击"添加翻译源"按钮
2. 在右侧面板编辑翻译源信息：
   - 名称：翻译源的显示名称
   - Base URL：API 地址（如 https://api.openai.com）
   - API Key：你的 API 密钥
   - Model：使用的模型名称（如 gpt-4o-mini）

**编辑翻译源**
1. 从左侧列表选择要编辑的翻译源
2. 在右侧面板修改信息
3. 点击"保存"或"应用"按钮

**删除翻译源**
1. 选择要删除的翻译源
2. 点击"删除"按钮
3. 注意：至少需要保留一个翻译源

**调整顺序**
- 使用"上移"和"下移"按钮调整翻译源的显示顺序

### 4. 网络代理

**启用代理**
1. 勾选"启用 HTTP 代理"
2. 输入代理地址（如 http://127.0.0.1）
3. 设置端口号（默认 8080）

**身份验证**
如果代理需要认证：
1. 勾选"需要身份验证"
2. 输入用户名和密码

### 5. 保存设置

- **保存**：保存设置并关闭窗口
- **应用**：应用设置但不关闭窗口（立即生效）
- **取消**：取消修改并关闭窗口

## 快捷键捕获说明

在快捷键捕获模式下：
- 支持的修饰键：Ctrl、Shift、Alt、Win
- 支持的主键：A-Z、0-9、F1-F12、Space、Enter
- 按 ESC 取消捕获
- 必须至少包含一个修饰键

## 代理设置说明

**支持的代理类型**：HTTP 代理

**代理格式**：
```
地址：http://127.0.0.1
端口：8080
```

**注意事项**：
- 代理地址必须包含协议（http://）
- 端口范围：1-65535
- 如果代理不需要认证，请不要勾选"需要身份验证"

## 翻译源配置示例

### OpenAI 官方
```
名称：OpenAI
Base URL：https://api.openai.com
API Key：sk-xxxxxxxxxxxxx
Model：gpt-4o-mini
```

### OpenAI 兼容服务
```
名称：自定义服务
Base URL：https://your-custom-api.com
API Key：your-api-key
Model：gpt-3.5-turbo
```

## 配置文件位置

设置文件保存在：
```
%AppData%/WordLens/settings.json
```

## 常见问题

**Q: 快捷键设置后不生效？**
A: 点击"应用"按钮使快捷键立即生效，或重启应用。

**Q: 翻译源测试失败？**
A: 检查：
- API Key 是否正确
- Base URL 是否可访问
- 网络连接是否正常
- 代理设置是否正确（如果启用）

**Q: 代理不工作？**
A: 确认：
- 代理服务器正在运行
- 地址和端口正确
- 如果需要认证，用户名密码正确

**Q: 如何恢复默认设置？**
A: 删除配置文件 `%AppData%/WordLens/settings.json`，应用会自动创建默认配置。

## 技术实现

### 架构设计
- **MVVM 模式**：使用 CommunityToolkit.Mvvm
- **依赖注入**：通过 Microsoft.Extensions.DependencyInjection
- **数据持久化**：JSON 序列化到 AppData 目录

### 主要组件
- `SettingsViewModel`：设置界面的 ViewModel
- `MainWindowView`：设置窗口 UI
- `SettingsService`：设置加载和保存服务
- `HotkeyService`：快捷键管理服务
- `TranslationService`：翻译服务（集成代理支持）

### 数据模型
```csharp
AppSettings
├── HotkeyConfig (快捷键配置)
├── TargetLanguage (目标语言)
├── SelectedProvider (选中的翻译源)
├── Providers (翻译源列表)
└── Proxy (代理配置)
    ├── Enabled
    ├── Address
    ├── Port
    ├── UseAuthentication
    ├── Username
    └── Password
```

## 更新日志

### v1.0.0
- ✅ 实现多翻译源管理
- ✅ 实现快捷键捕获设置
- ✅ 实现 HTTP 代理配置
- ✅ 实现目标语言选择
- ✅ 集成 Avalonia UI TabControl
- ✅ 实现设置的保存、加载和应用

## 贡献

欢迎提交问题和改进建议！