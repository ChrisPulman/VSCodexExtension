# VSCodexExtension Implementation Plan

> **For Hermes:** This repository implements the plan directly. Future work can use subagent-driven-development task-by-task.

**Goal:** Build a Visual Studio 2022/2026 extension that exposes the local OpenAI Codex experience in a docked ReactiveUI tool window with chat, plan/build workflows, approvals, skills, MCP servers, memory, image/file attachments, solution-aware context, sessions, usage/rate-limit telemetry, and provider/profile configuration.

**Architecture:** The VSIX is a thin Visual Studio shell integration over a reactive application core. The UI is WPF + ReactiveUI; service boundaries are `IObservable<T>`-first, so Codex output, approval requests, tool calls, memory changes, MCP server state, file search, and session telemetry compose as streams. Codex execution is provided by a JSONL Node bridge to `@openai/codex-sdk` with a CLI fallback for environments where only the local `codex` executable is available.

**Tech Stack:** C#/.NET Framework 4.7.2 VSIX, Visual Studio SDK, ReactiveUI, ReactiveUI.WPF, ReactiveUI.Extensions, Newtonsoft.Json, WPF, local Node.js bridge using `@openai/codex-sdk`, local Codex CLI fallback.

---

## Key Features

1. Docked Codex tool window in Visual Studio.
2. Codex SDK JSONL bridge with CLI fallback.
3. Chat, Plan, and Build modes.
4. Approval and sandbox controls.
5. Skills discovery/injection from `SKILL.md`.
6. MCP server discovery from `%USERPROFILE%\.codex\config.toml`.
7. User and workspace memory stores.
8. Solution-aware `@file` context injection.
9. Solution-aware `#` context references for selected Visual Studio code and current-solution files.
10. `/MCP` command workflow with server listing, tool discovery, and required/optional input prompting.
11. Exception/debug context assistant for Visual Studio break state, stack summary, and selected code.
12. Integrated test assistant and editor context menu for creating tests from selected code.
13. Plan creation that recommends sub-agent allocation, model choice, handoff order, and validation.
14. Main orchestration model, per-agent model selection, and budget-driven model mode.
15. Theme-aware WPF styling via Visual Studio environment resources.
16. Configurable prompt input area with standard text cut/copy/paste and image/document paste/drop attachment handling.
17. Image/document/file attachments.
18. Session persistence and resume-by-thread-id.
19. Multi-agent orchestration task handler with Planner, Architect, Builder, Reviewer, and Verifier roles.
20. Large-request task splitting into trackable sections with reactive status events.

## Verification

```bash
python3 scripts/validate_structure.py
node --check src/VSCodexExtension/Resources/codex-bridge.mjs
```

## Windows Build

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  VSCodexExtension.sln `
  /t:Restore,Build `
  /p:Configuration=Release
```
