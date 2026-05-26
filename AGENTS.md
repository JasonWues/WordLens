# Repository Guidelines

## Project Structure & Module Organization

WordLens is a cross-platform desktop translation app built with Avalonia and a Rust native helper. The solution is `WordLens.slnx`.

- `WordLens/`: main Avalonia app. UI files live in `Views/`, view models in `ViewModels/`, services in `Services/`, models in `Models/`, assets in `Assets/`, and provider code in `Providers/`.
- `WordLens.Abstractions/`: shared service contracts and platform-neutral models.
- `WordLens.Windows/`, `WordLens.Linux/`, `WordLens.Macos/`: platform-specific service implementations and native interop.
- `WordLens.Test/`: xUnit v3 unit tests for platform-neutral logic.
- `native/`: Rust `cdylib` crate used for screenshot and selected-text helpers.
- `artifacts/`: generated build output; do not treat it as source.

## Build, Test, and Development Commands

- `dotnet build`: builds the .NET solution and runs the MSBuild Rust build target.
- `dotnet test WordLens.Test/WordLens.Test.csproj`: runs the .NET unit tests.
- `dotnet run --project WordLens`: starts the desktop app locally.
- `dotnet publish WordLens/WordLens.csproj -c Release -r win-x64 -o ./publish/win-x64`: creates Windows release output.
- `cargo build` from `native/`: builds only the Rust helper library.
- `dotnet format --verify-no-changes --verbosity diagnostic`: verifies C# formatting.
- `cargo fmt --all -- --check` and `cargo clippy -- -D warnings`: verify Rust style and lints.

## Coding Style & Naming Conventions

Use 4-space indentation for C# and keep nullable annotations enabled. Follow existing Avalonia MVVM naming: `*View.axaml`, matching `*View.axaml.cs`, and `*ViewModel.cs`. Name service contracts as `I...Service` or platform backend interfaces, with implementations under `Services/Implementations/` or the matching platform project.

## Testing Guidelines

Unit tests live in `WordLens.Test/` and use xUnit v3. Name files after the unit under test, for example `OpenAIRequestArgumentsTests.cs`, and prefer behavior-focused test names. For UI or integration changes, also manually exercise the affected workflow: selected-text translation, OCR capture, settings persistence, popup placement, or TTS as applicable.

## Commit & Pull Request Guidelines

Recent history uses short imperative summaries in Chinese or English, for example `调整项目架构`, `Add clipboard monitor and hotkey backends`, and `Refactor Windows hotkey backend to Win32 API`.

Pull requests should include a concise description, affected platforms, verification commands, and screenshots or recordings for visible UI changes. Link related issues when available and call out any configuration or migration steps.

## Security & Configuration Tips

Do not commit API keys, local model endpoints, user settings, database files, or generated native binaries. Keep OpenAI-compatible provider configuration local to the running app environment.
