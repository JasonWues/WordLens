# Repository Guidelines

## Project Structure & Module Organization

WordLens is a cross-platform desktop translation app built with Avalonia and a Rust native helper. The solution entry point is `WordLens.slnx`.

- `WordLens/`: main Avalonia application; UI lives in `Views/`, view models in `ViewModels/`, services in `Services/`, provider integrations in `Providers/`, and bundled resources in `Assets/`.
- `WordLens.Abstractions/`: shared contracts and platform-neutral models.
- `WordLens.Windows/`, `WordLens.Linux/`, and `WordLens.Macos/`: platform-specific services and native integration.
- `WordLens.Test/`: xUnit tests for reusable application logic.
- `native/`: Rust `cdylib` for screenshot and selected-text helpers.
- `artifacts/`: generated build output; do not edit or commit it as source.

## Build, Test, and Development Commands

- `dotnet build`: build the complete solution and its Rust helper.
- `dotnet test WordLens.Test/WordLens.Test.csproj`: run the unit test suite.
- `dotnet run --project WordLens`: launch the desktop app locally.
- `cargo build --manifest-path native/Cargo.toml`: build only the native library.
- `dotnet format --verify-no-changes --verbosity diagnostic`: verify C# formatting.
- `cargo fmt --manifest-path native/Cargo.toml --all -- --check` and `cargo clippy --manifest-path native/Cargo.toml -- -D warnings`: validate Rust formatting and lints.

## Coding Style & Naming Conventions

Use four-space indentation for C# and keep nullable annotations enabled. Follow existing Avalonia MVVM names: `ExampleView.axaml`, `ExampleView.axaml.cs`, and `ExampleViewModel.cs`. Name service contracts `I...Service` or platform backend interfaces, and place implementations under `Services/Implementations/` or the appropriate platform project. Prefer established project patterns over new abstractions.

## Testing Guidelines

Tests use xUnit v3. Put tests in `WordLens.Test/`, name files after the unit under test (for example, `OpenAIRequestArgumentsTests.cs`), and use behavior-focused test method names. Add focused regression tests for logic changes. Manually exercise affected UI workflows, especially OCR capture, selected-text translation, settings persistence, popup placement, clipboard monitoring, and text-to-speech.

## Commit & Pull Request Guidelines

Use short, imperative commit subjects consistent with history, such as `Add provider endpoint presets and tests`. Keep each commit focused. Pull requests should summarize behavior changes, identify affected platforms, list verification commands, link related issues, and include screenshots or recordings for visible UI changes. Call out configuration or migration steps explicitly.

## Security & Configuration Tips

Never commit API keys, user settings, local model endpoints, database files, generated native binaries, or publish output. Keep provider credentials and machine-specific configuration local.
