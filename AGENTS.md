# Repository Guidelines

## Project Structure & Module Organization

WordLens is a cross-platform desktop translation app built with Avalonia and a Rust native helper. The solution is `WordLens.slnx`.

- `WordLens/`: main Avalonia app. UI is in `Views/`, view models in `ViewModels/`, services in `Services/`, models in `Models/`, provider code in `Providers/`, and assets in `Assets/`.
- `WordLens.Abstractions/`: shared service contracts and platform-neutral models.
- `WordLens.Windows/`, `WordLens.Linux/`, `WordLens.Macos/`: platform-specific services and native interop.
- `WordLens.Test/`: xUnit v3 tests for platform-neutral logic.
- `native/`: Rust `cdylib` crate for screenshot and selected-text helpers.
- `artifacts/`: generated build and publish output; do not treat it as source.

## Build, Test, and Development Commands

- `dotnet build`: builds the .NET solution and invokes the Rust helper build.
- `dotnet test WordLens.Test/WordLens.Test.csproj`: runs unit tests.
- `dotnet run --project WordLens`: starts the desktop app locally.
- `dotnet publish WordLens/WordLens.csproj -c Release -f net11.0-windows10.0.19041.0 -r win-x64 -o ./publish/win-x64`: creates a Windows release build.
- `cargo build` from `native/`: builds only the Rust helper.
- `dotnet format --verify-no-changes --verbosity diagnostic`, `cargo fmt --all -- --check`, and `cargo clippy -- -D warnings`: verify formatting and Rust lints.

## Coding Style & Naming Conventions

Use 4-space indentation for C# and keep nullable annotations enabled. Follow existing Avalonia MVVM naming: `*View.axaml`, matching `*View.axaml.cs`, and `*ViewModel.cs`. Name service contracts as `I...Service` or platform backend interfaces, with implementations under `Services/Implementations/` or the matching platform project. Prefer existing patterns over new abstractions.

## Testing Guidelines

Tests use xUnit v3 and live in `WordLens.Test/`. Name files after the unit under test, such as `OpenAIRequestArgumentsTests.cs`, and use behavior-focused test names. For UI or integration changes, manually exercise affected workflows: selected-text translation, OCR capture, settings persistence, popup placement, clipboard monitoring, TTS, and AOT publish when relevant.

## Commit & Pull Request Guidelines

Recent commits use short imperative summaries, sometimes in Chinese, for example `Add localization support and resources`, `fix ci build`, or `更新ci发布`. Pull requests should include a concise description, affected platforms, verification commands, and screenshots or recordings for visible UI changes. Link related issues and call out configuration or migration steps.

## Security & Configuration Tips

Do not commit API keys, local model endpoints, user settings, database files, generated native binaries, or publish output. Keep provider configuration local to the running app environment.

## Agent-Specific Instructions

When answering questions about libraries, frameworks, SDKs, APIs, CLI tools, or cloud services, use Context7 MCP for current documentation before responding. Do not use it for business-logic debugging, refactoring, code review, or general programming concepts.
