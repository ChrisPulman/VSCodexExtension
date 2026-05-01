# VSCodexExtension

A ReactiveUI-first Visual Studio 2022/2026 extension that hosts local OpenAI Codex inside a docked tool window.

## Capabilities

- Docked Codex chat inside Visual Studio.
- Chat / Plan / Build modes.
- Local Codex SDK bridge using `@openai/codex-sdk` with CLI fallback.
- Primary, failover, budget, and orchestration model selection with reasoning effort, verbosity, service tier, approval policy, sandbox mode, and profile controls.
- Model analytics that estimate request cost and recommend whether the cheaper budget model is appropriate.
- Skills index and per-run skill context injection.
- MCP server registry backed by `~/.codex/config.toml`.
- ReactiveMemory MCP is configured as the default durable memory system, with local/workspace JSON memory as a fallback cache.
- Solution-aware `@file` search and active-context capture.
- Solution-aware `#` references for selected Visual Studio code (`#selection`) and files from the current solution.
- `/MCP` prompt workflow that lists configured MCP servers, discovers tools, and prompts for required/optional tool inputs.
- Debug assistant context capture from Visual Studio break/exception/stack/selection state.
- Copilot-style Visual Studio menus for Ask, Explain, Fix, Review, Optimize, Documentation, Debug, Plan, and test creation from selection.
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

- `src/VSCodexExtension.slnx` — the only Visual Studio solution file, in XML `.slnx` format.
- `docs/PLAN.md` — feature plan and architecture.
- `src/VSCodexExtension` — VSIX project.
- `src/VSCodexExtension/Resources/codex-bridge.mjs` — JSONL bridge to Codex SDK.
- `scripts/validate_structure.py` — WSL-safe structural validation.

## Windows Build

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  src\VSCodexExtension.slnx `
  /t:Restore,Build `
  /p:Configuration=Release
```

## Local Codex Setup

```powershell
npm install -g @openai/codex @openai/codex-sdk
codex login
```

The extension reuses local Codex auth/provider config. Provider and MCP configuration should remain in `%USERPROFILE%\.codex\config.toml`.

On refresh, VSCodex ensures a default `[mcp_servers.reactivememory]` entry exists. If the ReactiveMemory source repo is present locally, it launches that project; otherwise it falls back to the `CP.ReactiveMemory.Mcp.Server` tool package identity so the user can install or override the command in Codex config.
