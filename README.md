# WordLens

WordLens 是一个跨平台桌面翻译工具，基于 Avalonia UI、.NET 11 和 Rust native helper 构建。它面向日常阅读、写作和跨语言资料处理：可以通过全局快捷键翻译当前选中文本，也可以框选屏幕区域进行 OCR，再把识别结果送入翻译窗口。

## 功能概览

- 划词翻译：读取当前选中文本并弹出翻译窗口。
- OCR 翻译：框选屏幕区域、识别文字、可手动修正后翻译。
- 多翻译源：支持多个 OpenAI 兼容源并行请求，也支持 DeepL。
- 流式输出：支持打字机式实时显示，可配置延迟和每次更新字符数。
- TTS 朗读：支持 OpenAI 兼容 `/v1/audio/speech` 和平台本地 TTS。
- 翻译历史：支持搜索、收藏、删除、清空和重新翻译。
- 剪贴板监听：复制文本后自动翻译，当前主要支持 Windows 后端。
- 本地自动化 API：仅监听 `127.0.0.1`，用于脚本或本机工具调用翻译。
- 欧路词典生词本同步：可将原文加入欧路生词本。
- 数据备份与恢复：备份设置、翻译历史数据库和截图缓存。
- 多语言 UI：支持简体中文、英文、日文；本地化资源使用 AOT 友好的 C# 资源表。
- NativeAOT 发布：Release 构建默认启用 `PublishAot`。

## 平台支持

| 功能 | Windows | Linux | macOS |
| --- | --- | --- | --- |
| Avalonia 桌面 UI | 支持 | 支持 | 支持 |
| 全局快捷键 | 支持 | 支持 | 支持 |
| 截图 / 区域 OCR | 支持 | 支持 | 支持截图，远程 OCR 可用 |
| 本地 OCR | Windows Runtime OCR | Tesseract CLI | 暂未接入 |
| 本地 TTS | Windows Runtime TTS | 暂未接入 | `/usr/bin/say` |
| 剪贴板监听 | 支持 | 暂未接入 | 暂未接入 |

远程翻译、远程 OCR 和 LLM TTS 依赖用户自行配置可用的 OpenAI 兼容服务或 DeepL。

## 构建要求

- .NET 11 SDK
- Rust toolchain
- Windows、macOS 或 Linux

MSBuild 会在构建主项目时自动执行 `cargo build`，并把 Rust 动态库复制到输出目录。Rust crate 位于 `native/`，同时生成 `cdylib` 和 `staticlib`，用于普通运行和可选的静态 native helper 发布。

Linux 构建 native 模块通常需要以下系统包：

```bash
sudo apt-get update
sudo apt-get install -y \
  pkg-config libclang-dev libdbus-1-dev libegl-dev libgbm-dev \
  libpipewire-0.3-dev libwayland-dev libx11-dev libxcb1-dev \
  libxi-dev libxrandr-dev
```

Linux 本地 OCR 还需要 Tesseract：

```bash
sudo apt-get install -y \
  tesseract-ocr tesseract-ocr-eng tesseract-ocr-chi-sim \
  tesseract-ocr-chi-tra tesseract-ocr-jpn tesseract-ocr-kor
```

## 构建、运行与发布

```bash
dotnet build
dotnet test WordLens.Test/WordLens.Test.csproj
dotnet run --project WordLens
```

仅构建 Rust native helper：

```bash
cd native
cargo build
```

Windows NativeAOT 发布示例：

```bash
dotnet publish WordLens/WordLens.csproj \
  -c Release \
  -f net11.0-windows10.0.19041.0 \
  -r win-x64 \
  -o ./publish/win-x64
```

如果希望把 Rust helper 静态链接进 NativeAOT 主程序，可启用：

```bash
dotnet publish WordLens/WordLens.csproj \
  -c Release \
  -f net11.0-windows10.0.19041.0 \
  -r win-x64 \
  -p:UseStaticNativeHelper=true \
  -o ./publish/win-x64-static
```

## 使用步骤

1. 启动 WordLens，在系统托盘中找到图标。
2. 打开“设置”，在“常规”页配置 UI 语言、字体、快捷键、本地 API、备份和欧路同步。
3. 在“翻译源”页添加至少一个启用的 OpenAI 兼容源或 DeepL 源。
4. 在“OCR”页配置远程视觉模型或平台本地 OCR。
5. 在“TTS”页启用并配置 LLM TTS 或系统 TTS。
6. 在任意应用中选中文本并按翻译快捷键，或按 OCR 快捷键框选屏幕区域。
7. 如需复制即翻译，可在托盘菜单中启用剪贴板监听。

## 本地自动化 API

本地 API 默认关闭，只监听 `127.0.0.1`，所有请求都需要 Bearer Token。

当前接口：

- `GET /api/v1/health`
- `GET /api/v1/settings/status`
- `POST /api/v1/translate`
- `POST /api/v1/window/translate`

示例：

```powershell
Invoke-RestMethod `
  -Uri http://127.0.0.1:49631/api/v1/translate `
  -Method Post `
  -Headers @{ Authorization = "Bearer <Token>" } `
  -ContentType "application/json" `
  -Body '{"text":"hello world","sourceLanguage":"auto","targetLanguage":"zh-CN"}'
```

## 配置和数据位置

运行时数据保存在用户配置目录：

- Windows: `%APPDATA%/WordLens/`
- macOS: `~/Library/Application Support/WordLens/`
- Linux: `~/.config/WordLens/`

主要文件和目录：

- `settings.json`: 应用设置和 provider 配置。
- `translation_history.db`: 翻译历史数据库。
- `Screenshots/`: OCR 截图缓存。
- `logs/`: ZLogger 日志文件。

不要提交本地设置、API Key、Local API Token、欧路 Token、数据库文件、截图缓存或发布产物。

## 项目结构

```text
WordLens.slnx
WordLens/              # Avalonia 主应用
WordLens.Abstractions/ # 平台中立抽象和服务契约
WordLens.Windows/      # Windows 平台服务
WordLens.Linux/        # Linux 平台服务
WordLens.Macos/        # macOS 平台服务
WordLens.Test/         # xUnit v3 测试
native/                # Rust native helper
artifacts/             # 生成产物
```

主应用内部常用目录：

- `Views/`: Avalonia 视图和窗口。
- `ViewModels/`: MVVM 状态和命令。
- `Services/`: 服务接口和实现。
- `Providers/`: 翻译源相关代码。
- `Models/`: 配置、请求和业务模型。
- `Infrastructure/`: Avalonia、HTTP、安全、截图等基础设施。
- `Assets/`: 图标、样式和静态资源。

## 技术栈

- .NET 11 / C#
- Avalonia 12
- Semi.Avalonia / Irihi.Ursa
- CommunityToolkit.Mvvm
- Microsoft.Extensions.DependencyInjection / HttpClientFactory
- ASP.NET Core Minimal APIs / Kestrel
- SQLite / Microsoft.Data.Sqlite
- Dapper / Dapper.AOT
- System.Text.Json source generation
- SharpHook
- SkiaSharp
- SoundFlow / MiniAudio
- ZLogger
- OpenAI 兼容 Chat Completions、Vision 和 Speech API
- DeepL API
- 欧路词典 OpenAPI
- Rust `xcap` 和 `selection`

## 开发检查

提交前建议运行：

```bash
dotnet build
dotnet test WordLens.Test/WordLens.Test.csproj
dotnet format --verify-no-changes --verbosity diagnostic
cd native
cargo build
cargo fmt --all -- --check
cargo clippy -- -D warnings
```

如果缺少 Rust 组件：

```bash
rustup component add rustfmt clippy
```

## English Summary

WordLens is a cross-platform desktop translation app built with Avalonia, .NET 11, and a Rust native helper. It supports selected-text translation, OCR capture, multiple translation providers, streaming output, TTS playback, translation history, clipboard monitoring, local automation APIs, Eudic vocabulary sync, backup/restore, multilingual UI, and NativeAOT release builds.

Use `dotnet build`, `dotnet test WordLens.Test/WordLens.Test.csproj`, and `dotnet run --project WordLens` for local development. Release builds use `dotnet publish`; Windows NativeAOT publishing should specify `-f net11.0-windows10.0.19041.0` and a runtime identifier such as `win-x64`.

## License

WordLens is licensed under the GNU General Public License v3.0 only (`GPL-3.0-only`). See `LICENSE` for the full license text.
