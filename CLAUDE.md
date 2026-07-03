# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

WordLens is a cross-platform desktop translation tool (selected-text translation + screen-region OCR translation) built with Avalonia UI on .NET 11 plus a Rust native helper. It runs as a tray app with global hotkeys, streams results from multiple translation providers in parallel, and ships as NativeAOT binaries. License: GPL-3.0-only. See `AGENTS.md` and `README.md` (Chinese) for additional detail; code comments and log messages are frequently in Chinese — match the surrounding style.

## Commands

A Rust toolchain is required even for .NET work: building `WordLens.csproj` runs `cargo build` via the `BuildRust` MSBuild target and copies the dylib to the output directory (this applies to `dotnet test` too, since tests reference the main project).

```bash
dotnet build                                      # whole solution (WordLens.slnx) + Rust helper
dotnet run --project WordLens                     # run the app (multi-targeted: add -f net11.0-windows10.0.19041.0 on Windows / -f net11.0 on Linux if asked to pick a framework)
dotnet test WordLens.Test/WordLens.Test.csproj    # all tests (xUnit v3)
dotnet test WordLens.Test/WordLens.Test.csproj --filter "FullyQualifiedName~HotkeyTests"          # one test class
dotnet test WordLens.Test/WordLens.Test.csproj --filter "FullyQualifiedName~HotkeyTests.MethodName" # one test

dotnet format --verify-no-changes --verbosity diagnostic   # C# format check

cd native && cargo build                          # Rust helper only
cd native && cargo fmt --all -- --check           # Rust format check
cd native && cargo clippy -- -D warnings          # Rust lints (CI-enforced)
```

Release publish is NativeAOT (`PublishAot=true` is forced in Release). Framework/RID pairs used by CI: `net11.0-windows10.0.19041.0`+`win-x64`, `net11.0`+`linux-x64`, `net11.0-macos`+`osx-x64`/`osx-arm64` (macOS needs `dotnet workload restore WordLens/WordLens.csproj`). Example:

```bash
dotnet publish WordLens/WordLens.csproj -c Release -f net11.0-windows10.0.19041.0 -r win-x64 -o ./publish/win-x64
```

Adding `-p:UseStaticNativeHelper=true` statically links the Rust `staticlib` into the AOT binary instead of shipping the dylib. Linux builds need system packages listed in `README.md`.

## Architecture

### Platform layering and DI

The composition root is `ConfigureServices` in `WordLens/App.axaml.cs`. Platform dispatch happens in two layers:

1. **Compile time (TFM → define → project reference)**: `net11.0-windows10.0.19041.0` defines `WINDOWS` and references `WordLens.Windows`; `net11.0-macos` defines `MACOS` and references `WordLens.Macos`; plain `net11.0` references both `WordLens.Linux` and `WordLens.Macos` and picks at runtime via `OperatingSystem.IsLinux()/IsMacOS()`.
2. **Registration**: each platform project exposes an `AddWordLens{Windows,Linux,Macos}()` extension that registers real backends; `App.axaml.cs` then uses `TryAddSingleton` to fill any gaps with `Unsupported*` no-op implementations (`UnsupportedLocalOcrBackend`, `UnsupportedLocalTtsBackend`, etc.).

The backend contracts implemented by platform projects live in `WordLens.Abstractions/Services` (`IHotkeyBackend`, `IClipboardMonitorBackend`, `ILocalOcrBackend`, `ILocalTtsBackend`, `ICursorPositionProvider`, `IStartupService`). App-level service interfaces (`ISettingsService`, `IOcrService`, `ITranslationHistoryService`, ...) live in `WordLens/Services` with implementations under `Services/Implementations`. Non-Windows platforms use SharpHook for hotkeys and Avalonia clipboard polling as the generic fallback.

### Rust native helper (`native/`)

A single crate built as both `cdylib` and `staticlib`, providing screenshot capture (`xcap`), selected-text reading (`selection`), and macOS-only Vision OCR (`objc2-vision`, gated by `cfg(target_os = "macos")`). C# bindings are in `WordLens/Native/*.cs` using `[LibraryImport]` against lib name `"native"`. Interop protocol: functions return status codes (or null pointers) on failure, the message is fetched via `get_last_native_error`, and every buffer/string returned by Rust must be freed with the matching `free_screenshot_buffer`/`free_c_string` export. All Rust entry points wrap work in `catch_unwind` — keep that pattern when adding exports. RID→Rust-target-triple mapping and the linker setup for static linking are in `WordLens/WordLens.csproj`.

### NativeAOT constraints (affects everyday changes)

- Any type serialized with System.Text.Json must be registered in `WordLens/SourceGenerationContext.cs` (`[JsonSerializable]`), including Local API contracts.
- SQLite access (translation history) goes through Dapper with **Dapper.AOT** interceptors.
- UI localization does not use resx: strings live in compiled tables in `WordLens/Infrastructure/Avalonia/LocalizationResources.cs` (zh-CN default, en, ja) and are applied to Avalonia resources at runtime — add new UI strings to all three tables.
- Avalonia compiled bindings are on by default (`AvaloniaUseCompiledBindingsByDefault`).

### Translation pipeline

`TranslationService` (`WordLens/Services/TranslationServices.cs`) loads `AppSettings`, decrypts each provider's API key with `EncryptionService`, constructs an `ITranslationProvider` per enabled provider (`OpenAITranslationProvider` for any OpenAI-compatible endpoint, `DeepLTranslationProvider`), and fans out requests in parallel. In streaming mode it returns `TranslationResult` objects immediately and mutates them from background tasks — all mutations of results (and anything ViewModel-observable) must go through `Dispatcher.UIThread`; the typewriter pacing lives in `StreamResultUpdater`. HTTP clients come from `ProxyAwareHttpClientFactory` to honor proxy settings. OCR follows the same provider idea: `OpenAIOcrService` (vision models) with optional local backends per platform.

### Local automation API

`Services/LocalApi/LocalApiService.cs` hosts ASP.NET Core Minimal APIs on Kestrel (via `FrameworkReference Microsoft.AspNetCore.App`), bound to `127.0.0.1` only with Bearer-token auth, started/reconfigured from settings. Endpoints: `/api/v1/health`, `/api/v1/settings/status`, `/api/v1/translate`, `/api/v1/window/translate`. `LocalApiBridge` decouples the HTTP layer from UI services.

### App lifecycle & data

Single-instance via named mutex in `Program.cs`; `ShutdownMode.OnExplicitShutdown` (tray app — windows are managed by `IWindowManagerService`); `--show-settings`/`--settings` arg opens settings on startup. Runtime data lives under the user config dir (`%APPDATA%/WordLens/` on Windows): `settings.json`, `translation_history.db`, `Screenshots/`, `logs/` (ZLogger rolling files).

## Conventions

- Avalonia MVVM naming: `*View.axaml` + `*View.axaml.cs` in `Views/`, `*ViewModel.cs` in `ViewModels/` (CommunityToolkit.Mvvm); UI toolkit is Semi.Avalonia + Irihi.Ursa.
- Service contracts are `I...Service` (app-level) or `I...Backend` (platform-level); prefer existing patterns over new abstractions.
- Tests (xUnit v3) cover platform-neutral logic only; name files after the unit under test with behavior-focused test names. UI/integration changes are verified manually (selected-text translation, OCR capture, settings persistence, popup placement, clipboard monitoring, TTS, AOT publish).
- Commits: short imperative summaries, sometimes in Chinese.
- Never commit API keys, tokens, local settings, databases, screenshot caches, generated native binaries, or publish output; `artifacts/` is generated output, not source.
- When answering questions about libraries, frameworks, SDKs, APIs, CLI tools, or cloud services, use Context7 MCP for current documentation first; don't use it for business-logic debugging, refactoring, or general concepts.
