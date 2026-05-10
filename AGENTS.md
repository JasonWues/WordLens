# Repository Guidelines

## Project Structure & Module Organization

WordLens is a desktop translation app built with Avalonia UI and a Rust native helper library. The solution entry is `WordLens.slnx`.

- `WordLens/` contains the .NET 10 Avalonia application.
- `WordLens/Views/` stores `.axaml` views and code-behind files.
- `WordLens/ViewModels/` contains MVVM state and commands.
- `WordLens/Models/`, `Services/`, `Messages/`, `Converter/`, `Util/`, and `Native/` hold domain models, service interfaces/implementations, messaging types, converters, helpers, and C# native bindings.
- `WordLens/Assets/` contains Avalonia resources such as icons.
- `native/` is the Rust `cdylib` crate for screenshot, selection, and OCR preprocessing.
- `.github/workflows/main.yml` defines CI checks and platform builds.

There is no dedicated test project yet. Add one as `WordLens.Tests/` when introducing testable behavior.

## Build, Test, and Development Commands

- `dotnet build` builds the app and automatically runs `cargo build` for `native/`.
- `dotnet run --project WordLens` runs the desktop app locally.
- `dotnet publish WordLens/WordLens.csproj -c Release -r win-x64 -o ./publish/win-x64` publishes Windows x64.
- `cd native; cargo build` builds only the Rust library.
- `cd native; cargo fmt --all -- --check` checks Rust formatting.
- `cd native; cargo clippy -- -D warnings` runs Rust lint checks.
- `dotnet format --verify-no-changes --verbosity diagnostic` checks C# formatting.

After adding tests, run `dotnet test`.

## Coding Style & Naming Conventions

Use nullable-aware C# with 4-space indentation. Name Avalonia views `*View.axaml` or `*WindowView.axaml`, with matching `.axaml.cs` files. Name view models `*ViewModel` and derive them from `ViewModelBase`. Prefer `[ObservableProperty]` and `[RelayCommand]` for MVVM boilerplate. Use compiled bindings with `x:DataType` in AXAML.

Rust uses edition 2024 and `cargo fmt`. Keep FFI-facing behavior small and isolated under `native/src/`.

## Testing Guidelines

No coverage threshold is enforced. For .NET, prefer focused unit tests around services, models, converters, and view-model command behavior. Name test files after the subject, for example `SettingsServiceTests.cs`. For Rust, add unit tests near the relevant module and run `cargo test` from `native/`.

## Commit & Pull Request Guidelines

Recent commits use short Chinese summaries, for example `更新readme` and `完善翻译历史`. Keep subjects concise and action-oriented. Pull requests should include a description, affected areas, verification steps, linked issues when applicable, and screenshots or recordings for UI changes. Mention Windows, Linux, or macOS testing when relevant.

## Security & Configuration Tips

Do not commit API keys, proxy credentials, local settings, `bin/`, `obj/`, `native/target/`, or publish outputs. Keep secrets in local configuration and avoid logging sensitive request data.
