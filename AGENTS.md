# Repository Guidelines

## Project Structure & Module Organization

WordLens is a cross-platform desktop translation app built with Avalonia and a Rust native helper. The solution file is `WordLens.slnx`.

- `WordLens/`: main Avalonia app. UI lives in `Views/`, view models in `ViewModels/`, services in `Services/`, models in `Models/`, assets in `Assets/`, and provider code in `Providers/`.
- `WordLens.Abstractions/`: shared service contracts and platform-neutral models.
- `WordLens.Windows/`, `WordLens.Linux/`, `WordLens.Macos/`: platform-specific services and native interop.
- `WordLens.Test/`: xUnit v3 tests for platform-neutral logic.
- `native/`: Rust `cdylib` crate for screenshot and selected-text helpers.
- `artifacts/`: generated build output; do not treat it as source.

## Build, Test, and Development Commands

- `dotnet build`: builds the .NET solution and invokes the Rust build target.
- `dotnet test WordLens.Test/WordLens.Test.csproj`: runs unit tests.
- `dotnet run --project WordLens`: starts the desktop app locally.
- `dotnet publish WordLens/WordLens.csproj -c Release -r win-x64 -o ./publish/win-x64`: creates Windows release output.
- `cargo build` from `native/`: builds only the Rust helper library.
- `dotnet format --verify-no-changes --verbosity diagnostic`: verifies C# formatting.
- `cargo fmt --all -- --check` and `cargo clippy -- -D warnings`: verify Rust style and lints.

## Coding Style & Naming Conventions

Use 4-space indentation for C# and keep nullable annotations enabled. Follow existing Avalonia MVVM naming: `*View.axaml`, matching `*View.axaml.cs`, and `*ViewModel.cs`. Name service contracts as `I...Service` or platform backend interfaces, with implementations under `Services/Implementations/` or the matching platform project.

## Testing Guidelines

Unit tests use xUnit v3 and live in `WordLens.Test/`. Name files after the unit under test, such as `OpenAIRequestArgumentsTests.cs`, and prefer behavior-focused test names. For UI or integration changes, manually exercise affected workflows such as selected-text translation, OCR capture, settings persistence, popup placement, clipboard monitoring, and TTS.

## Commit & Pull Request Guidelines

Recent history uses short imperative summaries in English or Chinese, for example `Add Eudic vocabulary sync support`, `fix ci build`, and `更新ci发布`. Pull requests should include a concise description, affected platforms, verification commands, and screenshots or recordings for visible UI changes. Link related issues when available and call out configuration or migration steps.

## Security & Configuration Tips

Do not commit API keys, local model endpoints, user settings, database files, or generated native binaries. Keep OpenAI-compatible provider configuration local to the running app environment.

## Agent-Specific Instructions

When answering questions about libraries, frameworks, SDKs, APIs, CLI tools, or cloud services, use Context7 MCP for current documentation before responding. Do not use it for business-logic debugging, refactoring, code review, or general programming concepts.
