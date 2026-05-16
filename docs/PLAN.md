# VSCodex Implementation Plan

## Current Direction

VSCodex is a classic in-process Visual Studio VSIX that hosts Codex through a docked ReactiveUI.WPF tool window. The extension keeps the active solution repository as the execution root, exposes Copilot-style editor and debug commands, and gives developers explicit control over model, failover, MCP, skills, memory, analytics, sandbox, and approval settings.

## Architecture

- `src/VSCodex.slnx` is the only solution entry point.
- `src/VSCodex/VSCodexPackage.cs` registers the package, menus, first-run tool-window launch, and command table.
- `WorkspaceContextService` resolves the loaded solution or active project to the containing Git repository root and builds a stable workspace identity without creating repository-local VSCodex metadata files.
- `CodexSdkJsonClient`, `CodexCliClient`, and `codex-bridge.mjs` run requests from the resolved repository root.
- `PromptBuilder` enriches requests with workspace identity, selected files, selected code, skills, MCP servers, memory, and ReactiveMemory hooks.
- `MemoryStore` keeps only a workspace-scoped in-memory display cache; durable memory is handled by ReactiveMemory.
- `SolutionLoadMonitorService` listens for Visual Studio solution events and schedules a delayed, throttled ReactiveMemory ProjectMiner-compatible scan for the active repository; the Memory tab exposes an explicit full scan action.

## UX Goals

- Keep the main conversation and prompt composer visible in narrow docked layouts.
- Move advanced settings into the collapsible controls panel so the main workflow stays focused.
- Use Visual Studio theme resources for all common controls.
- Provide prompt suggestions for `/`, `#`, and `@`.
- Keep settings locked while a task is running.
- Allow the prompt editor to resize by mouse while preserving a one-line minimum.
- Keep prompt resizing responsive by saving the final height only after mouse drag completion.
- Give MCP servers and tool invocation inputs enough space through resizable split panes.
- Support voice-to-text prompt entry when Windows speech recognition is available.

## Verification

Run the following before packaging or publishing:

```powershell
dotnet build src\VSCodex.slnx --configuration Release
dotnet test src\VSCodex.slnx --configuration Release --no-build
python scripts\validate_structure.py
```

The test suite includes VSIX surface checks, SDK bridge startup checks, Codex parser resilience checks, theme and menu assertions, workspace-root identity assertions, and Marketplace packaging checks.
