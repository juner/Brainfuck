# Changelog

All notable changes to this repository are documented in this file.

The format is based on Keep a Changelog.

## [Unreleased]

### Added
- Generator: Added C# language version check (BF0010) to warn if below C# 8.0.
- Generator: Added support for `System.IO.TextReader`/`TextWriter` input/output patterns.
- Generator: Added BF0009 (Hidden) diagnostic for unused input parameters.
- Generator: Significantly expanded samples and test coverage (UseConsole sample, more comprehensive tests).
- CI: Added release workflow to GitHub Actions.

### Changed
- Generator: Diagnostic messages and ID structure unified with the Piet project.
- Generator: Clarified signature validation and combination rules.
- All READMEs: Rewritten in English, reorganized, and expanded with install, usage, and API details.
- Build/package baseline: unified repository `Version` to `1.0.0`, unified `LangVersion` to C# 14, and incremented `AssemblyVersion` / `FileVersion` to `0.1.1.101`.

### Fixed
- Generator: Improved accuracy of duplicate/invalid input/output parameter detection.
- Tests: Target frameworks now auto-switch for Windows/non-Windows environments.

## [0.1.1-preview-1] - 2026-04-16

### Added
- Added Interpreter test project with option binding and command registration tests.
- Added dotnet tool E2E checks in CI for pack/install/run/parse flow (PR workflow).
- Added XML documentation enforcement for packable projects.

### Changed
- Replaced file-based sample app with a csproj-based sample that supports net8.0, net9.0, and net10.0.
- Aggregated generated methods into a single generated source file.
- Updated README files for pre-release consistency and usage guidance.

### Fixed
- Fixed duplicate generated source headers in aggregated generator output.
- Removed unnecessary System.Memory package references.
- Fixed `--syntax-increment-current` option registration in Interpreter CLI.

### Package Notes
- Generator: aggregation output and header deduplication updates.
- Interpreter: new tests, CLI option registration fix, and tool E2E CI coverage.
- Parser: README output text typo corrections.
- Processor: README sample updated to use `BrainfuckProcessor`.
