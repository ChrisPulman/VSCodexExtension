# VSCodexExtension

A ReactiveUI-first Visual Studio 2022/2026 extension that hosts local OpenAI Codex inside a docked tool window.

## Capabilities

- Docked Codex chat inside Visual Studio.
- Chat / Plan / Build modes.
- Local Codex SDK bridge using `@openai/codex-sdk` with CLI fallback.
- Model, reasoning effort, verbosity, service tier, approval policy, sandbox mode, and profile selection.
- Skills index and per-run skill context injection.
- MCP server registry backed by `~/.codex/config.toml`.
- Local + workspace memory stores.
- Solution-aware `@file` search and active-context capture.
- Solution-aware `#` references for selected Visual Studio code (`#selection`) and files from the current solution.
- `/MCP` prompt workflow that lists configured MCP servers, discovers tools, and prompts for required/optional tool inputs.
- Debug assistant context capture from Visual Studio break/exception/stack/selection state.
- Integrated test assistant for selected code, including editor context-menu command to create tests.
- Plan generation that recommends sub-agent usage and validation sequencing.
- Main orchestration model, per-agent model selection, and budget-driven model mode.
- Theme-aware WPF tool window using Visual Studio environment brushes.
- Configurable prompt input height with standard text cut/copy/paste plus pasted/dropped image and document attachments.
- Image/document/file attachments.
- Session persistence and resume-by-thread-id.
- Multi-agent orchestration with logical Planner, Architect, Builder, Reviewer, and Verifier agents.
- Orchestration task splitting for larger multi-step requests, with per-section status tracking.
- ReactiveUI command/state composition throughout.

## Layout

- `docs/PLAN.md` — feature plan and architecture.
- `src/VSCodexExtension` — VSIX project.
- `src/VSCodexExtension/Resources/codex-bridge.mjs` — JSONL bridge to Codex SDK.
- `scripts/validate_structure.py` — WSL-safe structural validation.

## Windows Build

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  VSCodexExtension.sln `
  /t:Restore,Build `
  /p:Configuration=Release
```

## Local Codex Setup

```powershell
npm install -g @openai/codex @openai/codex-sdk
codex login
```

The extension reuses local Codex auth/provider config. Provider and MCP configuration should remain in `%USERPROFILE%\.codex\config.toml`.
