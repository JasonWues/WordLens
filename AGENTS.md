# Repository Guidelines

## Project Structure & Module Organization

WordLens is an Avalonia desktop translation app with a Rust native helper library. The solution entry is `WordLens.slnx`.

- `WordLens/` contains the .NET 10 Avalonia app.
- `WordLens/Views/` and `WordLens/ViewModels/` hold AXAML UI, code-behind, and MVVM state.
- `WordLens/Models/`, `Services/`, `Messages/`, `Converter/`, `Util/`, and `Native/` hold domain, service, messaging, conversion, helper, and binding code.
- `WordLens/Assets/` stores shared styles, icons, and resources.
- `native/` is the Rust 2024 `cdylib` crate for screenshot and selection support.
- `.github/workflows/main.yml` defines formatting, linting, and platform builds.

## Build, Test, and Development Commands

- `dotnet build` builds the app and invokes the native build integration.
- `dotnet run --project WordLens` runs the desktop app locally.
- `dotnet publish WordLens/WordLens.csproj -c Release -r win-x64 -o ./publish/win-x64` publishes Windows x64.
- `cd native; cargo build` builds only the Rust helper library.
- `cd native; cargo fmt --all -- --check` checks Rust formatting.
- `cd native; cargo clippy -- -D warnings` runs Rust lints as errors.
- `dotnet format --verify-no-changes --verbosity diagnostic` checks C# formatting.

## Coding Style & Naming Conventions

Use nullable-aware C# with 4-space indentation. Name Avalonia views `*View.axaml` or `*WindowView.axaml`, with matching `.axaml.cs` files. Name view models `*ViewModel` and derive from `ViewModelBase`. Prefer `[ObservableProperty]` and `[RelayCommand]`. Use compiled bindings with `x:DataType` in AXAML.

Rust uses edition 2024 and `cargo fmt`. Keep FFI-facing behavior small and isolated under `native/src/`.

## Testing Guidelines

No dedicated test project or coverage threshold is currently enforced. When adding .NET tests, create `WordLens.Tests/` and name files after the subject, for example `SettingsServiceTests.cs`. Focus on services, models, converters, and view-model commands. For Rust, place unit tests near the relevant module and run `cargo test` from `native/`.

## Commit & Pull Request Guidelines

Recent commits use short, action-oriented summaries in English or Chinese, such as `Add sortable provider list; enable debug logging`, `开发TTS功能`, and `更新ci文件`. Keep subjects concise and scoped.

Pull requests should include a description, affected areas, verification steps, linked issues when applicable, and screenshots or recordings for UI changes. Mention platform testing when relevant.

## Security & Configuration Tips

Do not commit API keys, proxy credentials, local settings, `bin/`, `obj/`, `native/target/`, or publish outputs. Keep secrets in local configuration and avoid logging sensitive request data.

## Agent-Specific Instructions

Use Context7 MCP for library, framework, SDK, API, CLI, or cloud-service documentation questions. Skip it for refactoring, business logic debugging, and code review.
