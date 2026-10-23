# VSCodex

VSCodex is a Visual Studio extension that brings OpenAI Codex into the IDE as a docked, theme-aware developer tool window. It is designed for developers who want Copilot-style editor integration plus explicit control over model selection, failover behavior, MCP servers, skills, memory, prompt context, analytics, approvals, sandboxing, and task orchestration.

The extension is built as a classic in-process Visual Studio VSIX using the ReactiveUI.Wpf.Reactive compatibility package and System.Reactive. The solution entry point is `src/VSCodex.slnx`.

VSCodex provides a docked Codex conversation with typed MCP form approvals, UTF-8 process communication, searchable settings, bounded workspace context, server-backed chat history, managed local worktrees and opt-in local automations. See [the issue and validation report](docs/ISSUE-RESOLUTION.md) and [the Desktop parity assessment](docs/DESKTOP-PARITY.md) for implementation details and compatibility limits.

The conversation surface uses compact Visual Studio icons and tooltips, keeps tool/MCP/skill/file details collapsed, and shows the final assistant summary when a turn completes. Account quota windows are identified by their reported duration, so weekly-only accounts display correctly; percentages and reset times come from account telemetry. **Execution transport** selects Codex app-server or CLI only; CLI conversations retain the actual Codex thread for follow-ups and support Stop. Runtime account model discovery is authoritative when available, while bundled choices support offline setup. Model availability can vary by account, client and workspace; consult the [Codex models documentation](https://learn.chatgpt.com/docs/models) for current information.

History can import matching-workspace Codex threads, fork, pin, archive and restore them. Review active file opens Visual Studio's native Git HEAD diff. The Workspace panel also provides [managed local worktrees](docs/WORKTREES.md) and [opt-in local automations with result notifications](docs/AUTOMATIONS.md). Worktree archive and restore preserve staged and working files with recovery snapshots; automation jobs use the CLI in a read-only sandbox and run only while the extension tool window is loaded. These local capabilities have explicit lifecycle limits; the parity assessment distinguishes them from Desktop cloud, remote and private-service features.

## Requirements

- Visual Studio 2022 17.x or Visual Studio 2026 18.x on Windows. The VSIX manifest supports Visual Studio major versions 17 and 18.
- Native acceptance has been completed on Visual Studio 2026; Visual Studio 2022 runtime compatibility is not yet verified.
- Visual Studio SDK workload for building or debugging the extension.
- .NET Framework 4.8 runtime. The VSIX targets `net48` because in-process Visual Studio packages run on the Visual Studio .NET Framework host.
- .NET 10 runtime for the bundled ReactiveMemory MCP server, which runs in a separate process.
- Node.js LTS and npm on the same PATH seen by Visual Studio.
- Codex SDK package for the bundled JavaScript bridge:

```powershell
npm install -g @openai/codex-sdk
```

- Codex CLI for the default app-server transport, CLI-only conversations and local automations (install both packages for the complete feature set):

```powershell
npm install -g @openai/codex
codex login
```

VSCodex reads local Codex configuration from `%USERPROFILE%\.codex\config.toml`.

## Getting Started

1. Install Node.js LTS and restart Visual Studio so `node` and `npm` are available to the IDE process.
2. Install both `@openai/codex-sdk` and `@openai/codex` globally for the complete transport and automation feature set.
3. Authenticate Codex/OpenAI from PowerShell if your account requires it.
4. Install VSCodex from the Marketplace or build the VSIX locally.
5. Open Visual Studio. VSCodex opens on first run and can be reopened from the VSCodex, View, Tools, editor context, project, solution, error, and debug menus.
6. Open a solution or repository folder. VSCodex treats that opened project as the Codex project boundary and runs Codex from the resolved repository root.
7. Open the VSCodex tool window. Global configuration is available under **Tools > Options > VSCodex > General**.

## Build and Test

```powershell
.\build.cmd --target Validate --configuration Release
```

Package versions are managed centrally in `Directory.Packages.props`. NUKE is the build entry point and runs the repository-pinned MinVer CLI once before restore or compilation. The resulting SemVer is then passed to every solution build as `Version`, `PackageVersion`, `AssemblyVersion`, `FileVersion`, `InformationalVersion`, `MinVerVersionOverride`, and `VSCodexVersion`, preventing individual projects from recalculating different versions.

MinVer derives local and pull-request versions from `v`-prefixed Git tags and commit height. A release build supplies an explicit SemVer:

```powershell
.\build.cmd --target PackageVsix --configuration Release --sem-ver 0.5.0
```

VSIX identity versions must be numeric, so NUKE maps supported prerelease channels into ordered fourth-part ranges: `preview` uses `0-9999`, `alpha` uses `10000-19999`, `beta` uses `20000-29999`, `rc` uses `30000-39999`, and stable versions use `65535`. The full SemVer, including prerelease and build metadata, remains in the assembly informational version.

The Release build writes `VSCodex.vsix`, `Install-VSCodex.cmd`, and `Install-VSCodex.ps1` to `src/VSCodex/bin/Release/net48`; NUKE stages the verified unsigned package under `output/unsigned`. The VSIX includes the required `CP.ReactiveMemory.Mcp.Server` runtime. If double-clicking `VSCodex.vsix` does not open Visual Studio's installer because the Windows `.vsix` file association is broken, run `Install-VSCodex.cmd`; it resolves `VSIXInstaller.exe` from the installed Visual Studio instance and launches the installer directly. To launch the visible installer as part of a command-line Release build, add `/p:VSCodexLaunchVsixInstaller=true`.

Debugging from Visual Studio installs the VSIX into the Experimental instance through `scripts/install-vsix-experimental.ps1`. The project intentionally disables the older raw VSSDK deployment path and uses VSIXInstaller so command tables, runtime assemblies, and VSIX assets are installed consistently.

## Main Tool Window

The VSCodex tool window is the primary workflow surface. It includes:

- Project-scoped conversation history with creation and switching between multiple chats.
- A prompt composer with Enter using the configured follow-up behavior, Ctrl+Enter using the opposite Queue/Steer behavior while a turn is active, and Shift+Enter inserting a newline.
- Explicit Queue, Steer, Pause, Resume, and Stop controls. Stop interrupts only the active turn and preserves queued follow-ups.
- Voice-to-text prompt input on Windows installations with speech recognition and a microphone available.
- Quick actions for review, active errors, tests, planning, explain, fix, optimize, and documentation.
- Inline context suggestions for `/`, `#`, and `@`.
- A collapsible workspace panel for history, context, memory activity, agents, and attachments.
- Approval feedback for Codex command execution and file-change requests.
- Typed MCP elicitation forms with required-field validation and Accept, Decline and Cancel actions.
- Streaming assistant responses that stay expanded while generating; after completion, earlier details collapse and the latest nonempty summary is shown expanded.
- Current thread status and Codex rate-limit telemetry, labeled by the actual quota-window duration and showing only windows reported by the account.
- Searchable, grouped Visual Studio options with draft validation and Cancel, separate from the native Unified Settings surface.

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

**Tools > Options > VSCodex > General** controls how Codex is called:

- Mode: chat, plan, or build workflows.
- Primary model, failover model, and budget model.
- Budget-driven model selection.
- Reasoning effort, verbosity, approval policy, sandbox mode, profile, and service tier.
- Transport selection for SDK bridge and optional CLI fallback.

The options page is separate from the main tool window, matching the Codex application’s dedicated settings surface. It also controls whether Enter queues or steers an active turn; Ctrl+Enter always performs the opposite action.

The bundled offline defaults are GPT-6.1 Sol for primary, GPT-6 Sol for failover and GPT-6 Luna for budget selection. GPT-6 Astra, GPT-5.6 Sol, GPT-5.6 Terra and GPT-5.6 Luna are also available as bundled choices. At runtime, the account's Codex model catalog and reasoning capabilities take precedence. **Check setup** refreshes that catalog, excludes hidden models and updates the pickers. Unavailable previously shipped choices migrate to the runtime default, including explicit agent-role selections; blank agent models continue to inherit the run model. Custom IDs remain editable, and older API models remain usable when advertised by the runtime. Availability depends on the account, client and workspace. See the [official Codex models documentation](https://learn.chatgpt.com/docs/models) for current availability.

Offline reasoning choices run from `low` to `ultra` for Sol, Astra and Terra, and `low` to `max` for Luna. Runtime-advertised choices and defaults override these fallbacks. VSCodex validates the selected model and reasoning effort together before execution.

Tool-window setting changes are retained per Visual Studio solution under the VSCodex app-data workspace state. Global Visual Studio options remain the default profile for newly opened solutions.

## Codex SDK and CLI Transport

VSCodex includes the `@openai/codex-sdk` bridge under `Resources/codex-bridge.mjs`. The app-server transport uses the Codex CLI app-server; SDK transport is available as a fallback, and CLI-only conversations are also supported. At startup and before execution it verifies:

- Node.js is available.
- npm is available.
- `@openai/codex-sdk` can be resolved.
- the bundled bridge file exists.
- the `@openai/codex` CLI can be found for app-server, CLI-only and automation use.

If setup is incomplete, VSCodex shows Windows-specific instructions in the conversation and Visual Studio options page rather than failing silently.

## MCP Servers

The MCP tab reads and writes server configuration from `%USERPROFILE%\.codex\config.toml`. It can list configured servers, discover tools, prompt for required inputs, and insert MCP tool calls into the current prompt.

ReactiveMemory is a required VSCodex service. The versioned `CP.ReactiveMemory.Mcp.Server` runtime is bundled in the VSIX, its Codex-shared `[mcp_servers.cp-reactivememory-mcp-server]` entry is recreated or re-enabled when necessary, and it cannot be disabled or removed through VSCodex. A local source build is preferred during development; the bundled runtime is preferred after installation, with `dnx` retained as a compatibility fallback. VSCodex migrates the older `[mcp_servers.reactivememory]` entry instead of keeping two memory servers active.

When Visual Studio opens a solution or repository folder, VSCodex waits until startup has settled before running a small, throttled ProjectMiner-compatible scan. Full repository mining is available from the Memory workspace panel with the Scan project button, so Visual Studio load is not dominated by memory writes.

ReactiveMemory source: https://github.com/ChrisPulman/ReactiveMemory.MCP.Server

## Memory

Memory support is designed to reduce lost context across sessions:

- User memories capture durable preferences and recurring instructions.
- Workspace memories capture repository-specific facts.
- VSCodex calls ReactiveMemory before each request, injects recovered project memory into the Codex prompt, and writes a diary entry after meaningful responses complete.
- Pausing first interrupts the exact active Codex turn, then stores the correlated workspace, chat, thread, turn, operation, prompt, partial response, conversation context, and queued follow-ups. VSCodex reports the run as paused only after ReactiveMemory confirms durable storage.
- The tool window keeps an in-memory session cache for display and search, but durable memory is stored through ReactiveMemory instead of repository-local JSON files.

The Memory tab exposes explicit save actions, while the prompt builder also injects memory context automatically with minimal user input.

## Skills

The Skills tab lists available Codex skills and controls which skills are injected for a run. Skills can provide workflow instructions, tool usage patterns, and domain-specific context. VSCodex keeps skill selection explicit so a developer can choose the right behavior for a request instead of relying on hidden defaults.

## Analytics and Rate Limits

The Analytics tab estimates prompt size, output size, complexity, primary model cost, budget model cost, savings percentage, and recommended model. Cost estimates use standard API token prices; they are not subscription-credit or billing quotes. Service tiers, caching and long-context premiums can change actual charges. Current profiles follow the [OpenAI API model catalog](https://developers.openai.com/api/docs/models).

Rate limits are displayed from account telemetry when present and classified by the actual window duration. This supports weekly-only accounts and avoids showing placeholder windows when the account does not report them. Percentages and local reset times follow the latest received telemetry.

## Attachments

The prompt editor accepts file drops and pasted files where supported. Attachments are tracked in the Attachments tab and included in the prompt context where the active Codex transport can use them.

## Multi-Agent Orchestration

For larger tasks, VSCodex can split work across logical planner, architect, builder, reviewer, and verifier roles. The Agents tab controls role enablement, per-role model selection, orchestration model, budget-driven model mode, and maximum agent concurrency.

## Marketplace Delivery

The repository includes Marketplace packaging assets and separate validation and delivery workflows:

- `src/VSCodex/Resources/VSCodexIcon.svg` is the source icon artwork.
- `src/VSCodex/Resources/VSCodexIcon-128.png` is used as the Visual Studio Marketplace icon.
- `src/VSCodex/Resources/VSCodexIcon-256.png` is used as the Marketplace preview image.
- `marketplace/vs-publish.json` is the VSIX publish manifest and uses this README as the Marketplace overview.
- `.github/workflows/BuildOnly.yml` runs the complete NUKE validation graph for pull requests targeting `main`.
- `.github/workflows/BuildDeploy.yml` is manually triggered from `main`, requires an explicit SemVer, builds and tests through NUKE, signs `VSCodex.vsix` with Certum SimplySign, verifies the OPC package signature and certificate, and uploads only the signed VSIX.

`BuildDeploy` uses the protected `release` GitHub Environment and requires these repository or environment secrets:

- `CERTUM_USER_ID`: the Certum SimplySign account identifier.
- `CERTUM_OTP_URI`: the protected `otpauth://` TOTP URI.
- `CERTUM_CERT_THUMBPRINT`: the 40-character SHA-1 thumbprint of the Certum code-signing certificate.

The workflow pins the Windows SimplySign setup action and Microsoft Sign CLI version. It validates the OPC signature and signer, checks that signing preserved the VSIX payload and metadata, and smoke-tests the signed package with Visual Studio's VSIX Installer in an isolated root suffix. It does not create a GitHub release, push a NuGet package, or publish to the Visual Studio Marketplace. After a successful manual run, download and extract the `VSCodex-<SemVer>` GitHub artifact, then upload the inner signed `VSCodex.vsix` to the Marketplace. GitHub's downloaded artifact is a transport ZIP and must not itself be renamed to `.vsix`.

## License

MIT. See [LICENSE](LICENSE).

---

**VSCodex** - Visual Studio AI assistance with Codex, ReactiveMemory, MCP, and solution-scoped control.
