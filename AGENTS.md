# Repository Guidelines

## Project Structure & Module Organization

WordLens is an Avalonia desktop app backed by a Rust native library. The main C# project lives in `WordLens/`: UI in `Views/*.axaml`, view models in `ViewModels/`, models in `Models/`, services in `Services/`, converters in `Converter/`, and embedded resources in `Assets/`. The Rust helper crate is in `native/` and builds a `cdylib` consumed by the app. CI configuration is under `.github/workflows/`. There are no dedicated test projects yet.

## Build, Test, and Development Commands

- `dotnet build` builds the Avalonia app and automatically runs `cargo build` for `native/` through the MSBuild `BuildRust` target.
- `dotnet run --project WordLens` runs the desktop app locally.
- `dotnet publish WordLens/WordLens.csproj -c Release -r win-x64 -o ./publish/win-x64` creates a Windows release build; CI also uses `linux-x64`, `osx-x64`, and `osx-arm64`.
- `cargo build` from `native/` builds only the Rust library.

## Coding Style & Naming Conventions

Use C# nullable annotations and the existing MVVM pattern. Keep UI composition in `.axaml` views, state and commands in `*ViewModel.cs`, and external behavior behind service interfaces. Prefer CommunityToolkit.Mvvm attributes for observable properties and commands. Avalonia files use `.axaml`, compiled bindings, and `x:DataType`; avoid WPF-only APIs such as `DependencyProperty`, `Visibility`, and `pack://` resources. Use four-space indentation for C# and `cargo fmt` for Rust.

## Testing Guidelines

No automated test suite is currently checked in. Before submitting changes, run `dotnet build` and, for Rust changes, `cargo test` from `native/` if tests are added. Name future C# test projects with a `.Tests` suffix, for example `WordLens.Tests` with `SettingsServiceTests`.

## Formatting and Quality Checks

CI runs `dotnet format --verify-no-changes --verbosity diagnostic`, `cargo fmt --all -- --check`, and `cargo clippy -- -D warnings`. Run relevant checks locally before opening a pull request.

## Commit & Pull Request Guidelines

Recent history mixes Conventional Commit style (`refactor(window): ...`) with short imperative messages in English and Chinese. Prefer `type(scope): summary`, such as `fix(settings): encrypt imported api keys`. Pull requests should describe the change, list verification commands, link issues, and include screenshots for UI changes.

## Security & Configuration Tips

Do not commit API keys, proxy credentials, logs, publish output, or local IDE settings. Runtime settings are stored in user profile locations such as `%APPDATA%/WordLens/settings.json` on Windows, and logs are written outside the repository.
