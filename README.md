# VSCodex

VSCodex is a Visual Studio extension that brings OpenAI Codex into the IDE as a docked, theme-aware developer tool window. It is designed for developers who want Copilot-style editor integration plus explicit control over model selection, failover behavior, MCP servers, skills, memory, prompt context, analytics, approvals, sandboxing, and task orchestration.

The extension is built as a classic in-process Visual Studio VSIX using ReactiveUI.WPF, ReactiveUI.Extensions, and System.Reactive. The solution entry point is `src/VSCodex.slnx`.

## Requirements

- Visual Studio 2022 17.x or newer on Windows.
- Visual Studio SDK workload for building or debugging the extension.
- .NET Framework 4.8 runtime. The VSIX targets `net48` because in-process Visual Studio packages run on the Visual Studio .NET Framework host.
- Node.js LTS and npm on the same PATH seen by Visual Studio.
- Codex SDK bridge package:

```powershell
npm install -g @openai/codex-sdk
```

- Optional Codex CLI fallback:

```powershell
npm install -g @openai/codex
codex login
```

VSCodex reads local Codex configuration from `%USERPROFILE%\.codex\config.toml`.

## Getting Started

1. Install Node.js LTS and restart Visual Studio so `node` and `npm` are available to the IDE process.
2. Install `@openai/codex-sdk` globally.
3. Install `@openai/codex` globally if you want the CLI fallback transport.
4. Authenticate Codex/OpenAI from PowerShell if your account requires it.
5. Install VSCodex from the Marketplace or build the VSIX locally.
6. Open Visual Studio. VSCodex opens on first run and can be reopened from the VSCodex, View, Tools, editor context, project, solution, error, and debug menus.
7. Open the VSCodex tool window, select Settings, and run setup validation if prompted.

## Build and Test

```powershell
msbuild src\VSCodex.slnx /restore /p:Configuration=Release /m
dotnet test src\VSCodex.slnx --configuration Release --no-build
python scripts\validate_structure.py
```

Debugging from Visual Studio installs the VSIX into the Experimental instance through `scripts/install-vsix-experimental.ps1`. The project intentionally disables the older raw VSSDK deployment path and uses VSIXInstaller so command tables, runtime assemblies, and VSIX assets are installed consistently.

## Main Tool Window

The VSCodex tool window is the primary workflow surface. It includes:

- Conversation history with user, system, assistant, and error messages.
- A prompt composer with Ctrl+Enter to run and Esc to cancel.
- Quick actions for review, active errors, tests, planning, explain, fix, optimize, and documentation.
- Inline context suggestions for `/`, `#`, and `@`.
- A collapsible controls panel for settings, context, skills, MCP, analytics, memory, agents, and attachments.
- Current thread status and Codex rate-limit telemetry for five-hour and weekly windows when emitted by the SDK.

The tool window uses Visual Studio environment colors and includes the VSCodex icon in the header. It is laid out for docked use and constrains narrow views so the core prompt and run controls remain available.

## Prompt Context

VSCodex supports prompt tokens that map to Visual Studio workspace context:

- `#` inserts selected code and solution-aware references.
- `@` searches repository files and can open a file picker for references elsewhere on disk.
- `/` lists focused commands, settings, and tool surfaces.

These suggestions are resolved through the Visual Studio workspace and DTE services on the UI thread, then inserted into the prompt as stable references. Large selected code blocks are preserved for explicit code-selection commands.

## Editor and Debug Menus

VSCodex registers Copilot-style Visual Studio command surfaces:

- Editor selection context menu: Ask VSCodex, Explain, Fix, Review, Optimize, Generate Documentation, and Create Tests.
- Project, solution, and item context menus: open VSCodex, ask about selected items, create tests, and create plans.
- Error List and error correction menus: fix or debug the selected issue with VSCodex.
- Debug menu and exception break state: Debug Exception with VSCodex when Visual Studio is stopped on a runtime exception.

Commands use query-status routing so they appear in context-sensitive locations instead of being permanently visible everywhere.

## Models and Execution

The Settings panel controls how Codex is called:

- Mode: chat, plan, or build workflows.
- Primary model, failover model, and budget model.
- Budget-driven model selection.
- Reasoning effort, verbosity, approval policy, sandbox mode, profile, and service tier.
- Transport selection for SDK bridge and optional CLI fallback.

Settings are locked while a task is running so delayed binding updates cannot change model, MCP, or sandbox behavior mid-request.

## Codex SDK and CLI Transport

VSCodex prefers the `@openai/codex-sdk` bridge shipped in the VSIX under `Resources/codex-bridge.mjs`. At startup and before execution it verifies:

- Node.js is available.
- npm is available.
- `@openai/codex-sdk` can be resolved.
- the bundled bridge file exists.
- the optional `@openai/codex` CLI fallback can be found when configured.

If setup is incomplete, VSCodex shows Windows-specific instructions in the conversation and Settings panel rather than failing silently.

## MCP Servers

The MCP tab reads and writes server configuration from `%USERPROFILE%\.codex\config.toml`. It can list configured servers, discover tools, prompt for required inputs, and insert MCP tool calls into the current prompt.

ReactiveMemory is configured as the default durable memory server. VSCodex adds a `[mcp_servers.reactivememory]` entry if one is missing. If the ReactiveMemory source repo is present locally it can launch that project; otherwise it falls back to the `CP.ReactiveMemory.Mcp.Server` package identity so users can install or override the command.

ReactiveMemory source: https://github.com/ChrisPulman/ReactiveMemory.MCP.Server

## Memory

Memory support is designed to reduce lost context across sessions:

- User memories capture durable preferences and recurring instructions.
- Workspace memories capture repository-specific facts.
- Prompt builder hooks call ReactiveMemory status and prompt-reaction tools when available.
- Local JSON memory is used as a fallback cache when MCP memory is unavailable.

The Memory tab exposes explicit save actions, while the prompt builder also injects memory context automatically with minimal user input.

## Skills

The Skills tab lists available Codex skills and controls which skills are injected for a run. Skills can provide workflow instructions, tool usage patterns, and domain-specific context. VSCodex keeps skill selection explicit so a developer can choose the right behavior for a request instead of relying on hidden defaults.

## Analytics and Rate Limits

The Analytics tab estimates prompt size, output size, complexity, primary model cost, budget model cost, savings percentage, and recommended model. This helps decide whether a cheaper model is appropriate before sending the request.

Rate limits are displayed from real Codex SDK telemetry when present. VSCodex maps Codex primary telemetry to the five-hour window and secondary telemetry to the weekly window so the display matches the Codex rate-limit UI.

## Attachments

The prompt editor accepts file drops and pasted files where supported. Attachments are tracked in the Attachments tab and included in the prompt context where the active Codex transport can use them.

## Multi-Agent Orchestration

For larger tasks, VSCodex can split work across logical planner, architect, builder, reviewer, and verifier roles. The Agents tab controls role enablement, per-role model selection, orchestration model, budget-driven model mode, and maximum agent concurrency.


```

## License

MIT. See [LICENSE](LICENSE).

---

**VSCodex** - Empowering Development Automation with AI and Reactive Technology ⚡🏭