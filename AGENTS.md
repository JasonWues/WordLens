# Repository Guidelines

## Project Structure & Module Organization

WordLens is a .NET/Avalonia desktop application with shared UI code and platform-specific services. `WordLens/` contains startup code, AXAML views, view models, models, services, messages, converters, and assets. `WordLens.Abstractions/` contains shared contracts. `WordLens.Windows/`, `WordLens.Linux/`, and `WordLens.Macos/` contain platform adapters. `native/` is a Rust 2024 `cdylib` for screenshot and selection support.

There is currently no dedicated test project. Add tests in a sibling project such as `WordLens.Tests/` and include it in `WordLens.slnx`.

## Build, Test, and Development Commands

- `dotnet restore WordLens.slnx`: restores .NET dependencies.
- `dotnet build WordLens.slnx -c Debug`: builds all projects; `WordLens.csproj` also runs `cargo build` for `native/`, so Rust and Cargo must be installed.
- `dotnet run --project WordLens/WordLens.csproj -f net11.0-windows10.0.19041.0`: runs the Windows target locally; use `net11.0` for non-Windows targets.
- `dotnet publish WordLens/WordLens.csproj -c Release -f net11.0-windows10.0.19041.0`: creates a release build with the configured AOT settings.
- `dotnet format --verify-no-changes`: checks C# formatting.
- `cargo fmt --manifest-path native/Cargo.toml --all -- --check` and `cargo clippy --manifest-path native/Cargo.toml -- -D warnings`: check Rust formatting and lints.
- `cargo test --manifest-path native/Cargo.toml`: runs Rust tests.

## Coding Style & Naming Conventions

Use 4-space indentation for C# and Rust. Keep nullable annotations enabled and address warnings instead of suppressing them. C# types, view models, services, and AXAML views use `PascalCase`; interfaces use the existing `IServiceName` pattern. Match paired Avalonia files, for example `MainWindowView.axaml` and `MainWindowView.axaml.cs`. Prefer constructor injection through service abstractions instead of platform checks in UI code. Use compiled bindings with `x:DataType` where practical.

## Testing Guidelines

When adding .NET tests, use xUnit or NUnit and name files after the unit under test, for example `SettingsServiceTests.cs`. Use descriptive test names like `SaveAsync_ValidSettings_PersistsFile`. Cover service behavior, serialization, hotkey configuration, and native interop boundaries before UI-only details.

## Commit & Pull Request Guidelines

Recent commits use short imperative summaries in either Chinese or English, such as `完善本地TTS` and `Add clipboard monitor and hotkey backends`. Keep the first line concise and focused on one change. Pull requests should describe behavior changes, list manual verification commands, link related issues, and include screenshots or screen recordings for visible UI changes.

## Security & Configuration Tips

Do not commit API keys, local proxy values, generated user settings, or translation history databases. Keep platform-specific native calls inside the platform projects or `native/`, and expose them to the app through `WordLens.Abstractions/` contracts.

## Agent-Specific Instructions

Use Context7 MCP for library, framework, SDK, API, CLI, or cloud-service documentation questions. Skip it for refactoring, business logic debugging, and code review.
