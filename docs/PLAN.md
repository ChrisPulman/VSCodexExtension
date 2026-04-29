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
9. Image attachments.
10. Session persistence and resume-by-thread-id.

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
