# WordLens

WordLens is a cross-platform desktop translation tool built with Avalonia UI and a Rust native helper library.

WordLens 是一个基于 Avalonia UI 和 Rust 原生辅助库构建的跨平台桌面翻译工具。

## 中文说明

### 项目简介

WordLens 面向日常阅读、写作和跨语言资料处理场景。它可以通过全局快捷键翻译当前选中的文本，也可以通过 OCR 快捷键框选屏幕区域，识别图片中的文字后再翻译。

当前项目包含：

- 划词翻译
- 区域截图 OCR
- OCR 结果确认窗口
- 多翻译源并行请求
- OpenAI 兼容模型列表获取
- 流式翻译输出
- TTS 朗读（OpenAI 兼容语音接口 / 系统 TTS）
- 翻译历史记录
- 开机自启
- HTTP 代理配置（系统代理 / 手动代理 / 认证代理）
- Rust 原生截图和选中文本获取
- SkiaSharp OCR 图片预处理
- 日志记录

### 主要功能

#### 划词翻译

在任意应用中选中文本，按下翻译快捷键后，WordLens 会读取当前选中文本并打开翻译窗口。

翻译窗口支持：

- 源语言和目标语言选择
- 多翻译源结果并列显示
- 流式输出
- 原文复制
- 单条译文复制
- 原文和译文朗读（启用 TTS 后）
- 窗口置顶
- 将译文作为新的原文继续翻译
- 交换源语言和目标语言

#### OCR 翻译

按下 OCR 快捷键后，WordLens 会打开屏幕区域选择遮罩。框选区域后会弹出 OCR 结果窗口：

- 左侧显示截取的图片
- 右侧显示识别到的文字
- 可手动修正识别文本
- 可重新识别
- 可将识别文本发送到翻译窗口

OCR 使用单独的 OCR 源配置，不占用翻译源列表。OCR 源可以是 OpenAI 兼容视觉模型，也可以是平台本地 OCR 后端：Windows 使用系统 OCR，Linux 使用 `tesseract` 命令行后端。

#### SkiaSharp OCR 预处理

OCR 请求前会通过 C# SkiaSharp 对截图做预处理：

- BGRA 转灰度
- 对比度拉伸
- 轻量锐化
- 小图放大
- PNG 编码

如果预处理失败，程序会自动回退到原始截图编码，不会阻断 OCR 流程。

#### 多翻译源

WordLens 支持配置多个 OpenAI 兼容翻译源。触发翻译时，所有已启用的翻译源会并行请求，单个翻译源失败不会影响其他翻译源。

可配置项包括：

- 名称
- Base URL
- API Key
- 模型名称
- Request Arguments JSON
- 是否启用

应用会尝试从 OpenAI 兼容接口获取模型列表；获取失败时仍可手动输入模型名称。

#### 翻译历史

成功翻译后会写入本地历史记录。历史窗口支持：

- 查看历史
- 搜索历史
- 删除记录
- 清空历史
- 收藏记录
- 从历史记录重新翻译

#### TTS 朗读

WordLens 支持在设置页的 `TTS` 标签中配置朗读源：

- OpenAI 兼容语音接口：调用 `/v1/audio/speech`，支持 Base URL、API Key、模型、音色、语速和 Request Arguments JSON。
- Windows 本地 TTS：使用系统 WinRT `SpeechSynthesizer`。
- macOS 本地 TTS：调用系统 `/usr/bin/say`。
- 音频播放：LLM TTS 返回的 WAV 音频通过 SoundFlow / MiniAudio 播放。

Linux 当前没有内置本地 TTS 后端，可使用 OpenAI 兼容语音接口。

### 构建要求

- .NET 11 SDK
- Rust toolchain
- Windows / macOS / Linux

项目构建时会通过 MSBuild 自动执行 `cargo build`，并把 native 动态库复制到输出目录。

Linux 上构建 Rust native 模块需要安装截图和桌面集成相关的系统开发包。Debian / Ubuntu 示例：

```bash
sudo apt-get update
sudo apt-get install -y \
  pkg-config \
  libclang-dev \
  libdbus-1-dev \
  libegl-dev \
  libgbm-dev \
  libpipewire-0.3-dev \
  libwayland-dev \
  libx11-dev \
  libxcb1-dev \
  libxi-dev \
  libxrandr-dev
```

如需在 Linux 上使用本地 OCR，还需要安装 Tesseract OCR 和对应语言包：

```bash
sudo apt-get install -y \
  tesseract-ocr \
  tesseract-ocr-eng \
  tesseract-ocr-chi-sim \
  tesseract-ocr-chi-tra \
  tesseract-ocr-jpn \
  tesseract-ocr-kor
```

### 构建和运行

```bash
dotnet build
dotnet run --project WordLens
```

仅构建 Rust native 模块：

```bash
cd native
cargo build
```

发布 Windows 版本示例：

```bash
dotnet publish WordLens/WordLens.csproj -c Release -f net11.0-windows10.0.19041.0 -r win-x64 -o ./publish/win-x64
```

### 使用步骤

1. 启动应用后，在系统托盘中找到 WordLens。
2. 右键托盘图标，打开“设置”。
3. 在“常规”页配置界面语言、开机自启、翻译快捷键和 OCR 快捷键。
4. 在“翻译源”页配置至少一个启用的翻译源。
5. 在“OCR”页配置 OCR 源。
6. 如需朗读，在“TTS”页启用并配置 LLM TTS 或系统 TTS。
7. 在任意应用中选中文本并按翻译快捷键，或按 OCR 快捷键框选屏幕区域。

### 配置和数据位置

运行时配置保存在用户目录下：

- Windows: `%APPDATA%/WordLens/settings.json`
- macOS: `~/Library/Application Support/WordLens/settings.json`
- Linux: `~/.config/WordLens/settings.json`

日志目录：

- Windows: `%APPDATA%/WordLens/logs/`
- macOS: `~/Library/Application Support/WordLens/logs/`
- Linux: `~/.config/WordLens/logs/`

翻译历史数据库：

- Windows: `%APPDATA%/WordLens/translation_history.db`
- macOS: `~/Library/Application Support/WordLens/translation_history.db`
- Linux: `~/.config/WordLens/translation_history.db`

截图临时文件：

- Windows: `%APPDATA%/WordLens/Screenshots/`
- macOS: `~/Library/Application Support/WordLens/Screenshots/`
- Linux: `~/.config/WordLens/Screenshots/`

### 技术架构

#### C# / Avalonia

- UI 和窗口：`WordLens/Views/`
- ViewModel：`WordLens/ViewModels/`
- 服务接口：`WordLens/Services/`
- 服务实现：`WordLens/Services/Implementations/`
- 数据模型：`WordLens/Models/`
- Native P/Invoke wrapper：`WordLens/Native/`

#### Rust native

Rust crate 位于 `native/`，当前拆分为：

- `lib.rs`：FFI 导出入口
- `buffers.rs`：FFI buffer 和内存释放
- `error.rs`：native error 和 C string 管理
- `selection_text.rs`：选中文本获取
- `screenshot.rs`：跨平台截图和虚拟屏幕边界

### 技术栈

- .NET 11 / C#
- Avalonia 12
- Semi.Avalonia / Irihi.Ursa
- CommunityToolkit.Mvvm
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Http
- System.Text.Json source generation
- SharpHook
- SQLite / Microsoft.Data.Sqlite
- Dapper / Dapper.AOT
- ZLogger
- SkiaSharp
- SoundFlow / MiniAudio
- OpenAI 兼容 Chat Completions、Vision 和 Speech 接口
- DeepL 翻译接口
- Microsoft.Windows.CsWin32 / Windows Runtime OCR 与 TTS
- Linux Tesseract OCR CLI
- Rust `xcap`
- Rust `selection`

### 当前限制

- 远程 OCR、翻译和 LLM TTS 依赖 OpenAI 兼容接口或 DeepL 等外部服务，需要自行配置可用服务。
- 本地 OCR 当前支持 Windows 系统 OCR 和 Linux Tesseract OCR；Linux 需要系统中可执行 `tesseract`，并安装所需语言包。macOS 本地 OCR 尚未接入。
- Linux 暂未提供内置本地 TTS 后端，可使用 OpenAI 兼容语音接口。
- API Key 当前存储在本地配置中，并经过简单加密/混淆；后续更适合改为系统凭据库。
- 单元测试位于 `WordLens.Test/`。
- Rust 格式化和 clippy 需要本机安装 `rustfmt` 和 `clippy` 组件。

### 开发建议

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

如果本机没有安装 Rust 格式化或 clippy 组件，可通过 rustup 安装：

```bash
rustup component add rustfmt clippy
```

## English

### Overview

WordLens is a desktop translation tool for reading, writing, and working with multilingual content. It can translate selected text through a global hotkey, and it can also capture a screen region, run OCR, let you review the recognized text, and send it to the translation window.

Current features include:

- Selected-text translation
- Region screenshot OCR
- OCR result review window
- Parallel translation with multiple providers
- OpenAI-compatible model list loading
- Streaming translation output
- TTS playback with OpenAI-compatible speech APIs or system TTS
- Translation history
- Auto-start on login
- HTTP proxy support, including system proxy, manual proxy, and authenticated proxy
- Rust native screenshot and selected-text extraction
- SkiaSharp OCR image preprocessing
- Structured logging

### Selected-Text Translation

Select text in any application and press the translation hotkey. WordLens reads the selected text and opens the translation window.

The translation window supports:

- Source and target language selection
- Multiple provider results
- Streaming output
- Copy source text
- Copy individual translations
- Speak source text and translations when TTS is enabled
- Always-on-top mode
- Use a translation as the new source text
- Swap source and target languages

### OCR Translation

Press the OCR hotkey to open the screen capture overlay. After selecting a region, WordLens opens an OCR result window:

- The captured image is shown on the left
- Recognized text is shown on the right
- Recognized text can be edited before translation
- OCR can be run again on the same image
- The recognized text can be sent to the translation window

OCR uses separate OCR provider settings and does not consume entries from the translation provider list. OCR providers can use OpenAI-compatible vision models or platform-local OCR backends: Windows uses system OCR, and Linux uses the `tesseract` command-line backend.

### SkiaSharp OCR Preprocessing

Before sending an image to OCR, WordLens preprocesses it with C# SkiaSharp:

- BGRA to grayscale conversion
- Contrast stretching
- Light sharpening
- Upscaling for small images
- PNG encoding

If preprocessing fails, WordLens falls back to the original screenshot encoding so OCR remains usable.

### Multiple Translation Providers

WordLens supports multiple OpenAI-compatible translation providers. When translation starts, all enabled providers are requested in parallel. A failure in one provider does not block the others.

Provider configuration includes:

- Name
- Base URL
- API key
- Model name
- Request Arguments JSON
- Enabled state

WordLens attempts to load the model list from compatible providers. If loading fails, the model name can still be entered manually.

### Translation History

Successful translations are saved locally. The history window supports:

- Browsing history
- Searching history
- Deleting entries
- Clearing all entries
- Favorites
- Translating again from history

### TTS Playback

WordLens supports speech providers in the `TTS` settings tab:

- OpenAI-compatible speech APIs: calls `/v1/audio/speech` and supports Base URL, API key, model, voice, speed, and Request Arguments JSON.
- Windows local TTS: uses the system WinRT `SpeechSynthesizer`.
- macOS local TTS: uses the system `/usr/bin/say` command.
- Audio playback: WAV audio returned by LLM TTS is played through SoundFlow / MiniAudio.

Linux currently has no built-in local TTS backend; use an OpenAI-compatible speech provider instead.

### Requirements

- .NET 11 SDK
- Rust toolchain
- Windows / macOS / Linux

The C# project automatically runs `cargo build` through MSBuild and copies the native dynamic library to the output directory.

Linux builds need native development packages for screenshot capture and desktop integration. Debian / Ubuntu example:

```bash
sudo apt-get update
sudo apt-get install -y \
  pkg-config \
  libclang-dev \
  libdbus-1-dev \
  libegl-dev \
  libgbm-dev \
  libpipewire-0.3-dev \
  libwayland-dev \
  libx11-dev \
  libxcb1-dev \
  libxi-dev \
  libxrandr-dev
```

Linux local OCR also requires Tesseract OCR and the language packages you want to recognize:

```bash
sudo apt-get install -y \
  tesseract-ocr \
  tesseract-ocr-eng \
  tesseract-ocr-chi-sim \
  tesseract-ocr-chi-tra \
  tesseract-ocr-jpn \
  tesseract-ocr-kor
```

### Build and Run

```bash
dotnet build
dotnet run --project WordLens
```

Build only the Rust native module:

```bash
cd native
cargo build
```

Publish example for Windows:

```bash
dotnet publish WordLens/WordLens.csproj -c Release -f net11.0-windows10.0.19041.0 -r win-x64 -o ./publish/win-x64
```

### Usage

1. Start WordLens and find it in the system tray.
2. Right-click the tray icon and open Settings.
3. Configure UI language, auto-start, translation hotkey, and OCR hotkey in the General tab.
4. Configure at least one enabled translation provider in the Providers tab.
5. Configure the OCR provider in the OCR tab.
6. Enable and configure an LLM speech provider or system TTS provider in the TTS tab if speech playback is needed.
7. Select text in any app and press the translation hotkey, or press the OCR hotkey and select a screen region.

### Config and Data Locations

Settings:

- Windows: `%APPDATA%/WordLens/settings.json`
- macOS: `~/Library/Application Support/WordLens/settings.json`
- Linux: `~/.config/WordLens/settings.json`

Logs:

- Windows: `%APPDATA%/WordLens/logs/`
- macOS: `~/Library/Application Support/WordLens/logs/`
- Linux: `~/.config/WordLens/logs/`

Translation history database:

- Windows: `%APPDATA%/WordLens/translation_history.db`
- macOS: `~/Library/Application Support/WordLens/translation_history.db`
- Linux: `~/.config/WordLens/translation_history.db`

Temporary screenshots:

- Windows: `%APPDATA%/WordLens/Screenshots/`
- macOS: `~/Library/Application Support/WordLens/Screenshots/`
- Linux: `~/.config/WordLens/Screenshots/`

### Architecture

#### C# / Avalonia

- UI and windows: `WordLens/Views/`
- ViewModels: `WordLens/ViewModels/`
- Service interfaces: `WordLens/Services/`
- Service implementations: `WordLens/Services/Implementations/`
- Models: `WordLens/Models/`
- Native P/Invoke wrappers: `WordLens/Native/`

#### Rust Native Module

The Rust crate lives in `native/` and is split into:

- `lib.rs`: FFI export layer
- `buffers.rs`: FFI buffers and memory release
- `error.rs`: native error and C string management
- `selection_text.rs`: selected-text extraction
- `screenshot.rs`: cross-platform screenshots and virtual screen bounds

### Tech Stack

- .NET 11 / C#
- Avalonia 12
- Semi.Avalonia / Irihi.Ursa
- CommunityToolkit.Mvvm
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Http
- System.Text.Json source generation
- SharpHook
- SQLite / Microsoft.Data.Sqlite
- Dapper / Dapper.AOT
- ZLogger
- SkiaSharp
- SoundFlow / MiniAudio
- OpenAI-compatible Chat Completions, Vision, and Speech APIs
- DeepL translation API
- Microsoft.Windows.CsWin32 / Windows Runtime OCR and TTS
- Linux Tesseract OCR CLI
- Rust `xcap`
- Rust `selection`

### Current Limitations

- Remote OCR, translation, and LLM TTS depend on OpenAI-compatible APIs or external services such as DeepL. You need to configure your own available services.
- Local OCR currently supports Windows system OCR and Linux Tesseract OCR. Linux requires a runnable `tesseract` command and installed language packages. macOS local OCR is not connected yet.
- Linux currently has no built-in local TTS backend; use an OpenAI-compatible speech API instead.
- API keys are currently stored locally with simple encryption/obfuscation. A system credential store would be a better long-term option.
- Unit tests live in `WordLens.Test/`.
- Rust formatting and clippy checks require the `rustfmt` and `clippy` components.

### Development Checks

Recommended checks before submitting changes:

```bash
dotnet build
dotnet test WordLens.Test/WordLens.Test.csproj
dotnet format --verify-no-changes --verbosity diagnostic
cd native
cargo build
cargo fmt --all -- --check
cargo clippy -- -D warnings
```

Install Rust formatting and clippy components if needed:

```bash
rustup component add rustfmt clippy
```

## License

MIT License. See `LICENSE` if present in this repository.
