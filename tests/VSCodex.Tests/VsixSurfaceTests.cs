using System.Diagnostics;
using System.Xml.Linq;
using TUnit.Core;

namespace VSCodex.Tests;

public sealed class VsixSurfaceTests
{
    private static readonly XNamespace Vsct = "http://schemas.microsoft.com/VisualStudio/2005-10-18/CommandTable";
    private static readonly string RepositoryRoot = LocateRepositoryRoot();

    [Test]
    public void Solution_includes_TUnit_MTP_test_project()
    {
        var slnx = ReadText("src/VSCodex.slnx");
        RequireContains(slnx, "../tests/VSCodex.Tests/VSCodex.Tests.csproj", "Solution must include the TUnit/MTP regression test project.");

        var testProject = XDocument.Load(PathFor("tests/VSCodex.Tests/VSCodex.Tests.csproj"));
        RequireElementValue(testProject, "UseMicrosoftTestingPlatformRunner", "true");
        RequirePackageReference(testProject, "TUnit");
        RequirePackageReference(testProject, "Microsoft.Testing.Platform");
    }

    [Test]
    public void VSCodex_tool_window_is_available_from_Extensions_View_Tools_Debug_and_context_menus()
    {
        var vsct = XDocument.Load(PathFor("src/VSCodex/Commands/CodexCommands.vsct"));
        var commandSource = ReadText("src/VSCodex/Commands/OpenVSCodexToolWindowCommand.cs");
        var assistantSource = ReadText("src/VSCodex/Services/CodingAssistantContextService.cs");
        var menuBridge = ReadText("src/VSCodex/Services/VisualStudioMenuIntegrationService.cs");
        var packageSource = ReadText("src/VSCodex/VSCodexPackage.cs");

        RequireMenuParent(vsct, "VSCodexTopLevelMenu", "guidCommandSet", "VSCodexExtensionsMenuGroup");
        RequireMenuParent(vsct, "VSCodexViewMenu", "guidCommandSet", "CodexViewMenuGroup");
        RequireMenuParent(vsct, "VSCodexEditorContextMenu", "guidCommandSet", "VSCodexCodeWindowContextMenuGroup");
        RequireGroupParent(vsct, "VSCodexExtensionsMenuGroup", "guidSHLMainMenu", "IDG_VS_MM_TOOLSADDINS");
        RequireGroupParent(vsct, "VSCodexTopLevelMenuGroup", "guidCommandSet", "VSCodexTopLevelMenu");
        RequireGroupParent(vsct, "CodexToolsMenuGroup", "guidSHLMainMenu", "IDM_VS_MENU_TOOLS");
        RequireGroupParent(vsct, "CodexViewMenuGroup", "guidSHLMainMenu", "IDM_VS_MENU_VIEW");
        RequireGroupParent(vsct, "VSCodexViewMenuCommandsGroup", "guidCommandSet", "VSCodexViewMenu");
        RequireGroupParent(vsct, "CodexProjectContextMenuGroup", "guidSHLMainMenu", "IDM_VS_CTXT_PROJNODE");
        RequireGroupParent(vsct, "CodexSolutionContextMenuGroup", "guidSHLMainMenu", "IDM_VS_CTXT_SOLNNODE");
        RequireGroupParent(vsct, "CodexItemContextMenuGroup", "guidSHLMainMenu", "IDM_VS_CTXT_ITEMNODE");
        RequireGroupParent(vsct, "CodexEditorContextMenuGroup", "guidSHLMainMenu", "IDM_VS_CTXT_CODEWIN");
        RequireGroupParent(vsct, "VSCodexCodeWindowContextMenuGroup", "guidSHLMainMenu", "IDM_VS_CTXT_CODEWIN");
        RequireGroupParent(vsct, "VSCodexEditorAllContextMenuGroup", "guidSHLMainMenu", "IDM_VS_CTXT_EDITOR_ALL");
        RequireGroupParent(vsct, "VSCodexEditorContextMenuActionsGroup", "guidCommandSet", "VSCodexEditorContextMenu");
        RequireGroupParent(vsct, "CodexDebugMenuGroup", "guidSHLMainMenu", "IDM_VS_MENU_DEBUG");
        RequireGroupParent(vsct, "CodexErrorListContextMenuGroup", "guidSHLMainMenu", "IDM_VS_CTXT_ERRORLIST");
        RequireGroupParent(vsct, "CodexErrorCorrectionContextMenuGroup", "guidSHLMainMenu", "IDM_VS_CTXT_ERROR_CORRECTION");

        RequireButtonParent(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexViewMenuGroup");
        RequireButtonString(vsct, "OpenToolWindowCommandId", "ButtonText", "VSCodex");
        RequireButtonString(vsct, "OpenToolWindowCommandId", "CommandName", "View.VSCodex");
        RequireButtonString(vsct, "OpenToolWindowCommandId", "LocCanonicalName", "View.VSCodex");
        RequireButtonDoesNotHaveCommandFlag(vsct, "OpenToolWindowCommandId", "DefaultInvisible");
        RequireButtonDoesNotHaveCommandFlag(vsct, "OpenToolWindowCommandId", "DynamicVisibility");
        RequireKeyBinding(vsct, "OpenToolWindowCommandId", "guidVSStd97", "0x43", "Control Shift");
        RequireDoesNotDefineIdSymbol(vsct, "IDM_VS_MENU_VIEW");
        RequireDoesNotDefineIdSymbol(vsct, "IDG_VS_WNDO_OTRWNDWS1");
        RequireDoesNotDefineIdSymbol(vsct, "IDM_VS_MENU_EXTENSIONS");
        RequireButtonParent(vsct, "OpenOptionsCommandId", "guidCommandSet", "VSCodexTopLevelMenuGroup");
        RequireButtonParent(vsct, "AskCodexCommandId", "guidCommandSet", "VSCodexEditorContextMenuActionsGroup");
        RequireButtonParent(vsct, "ExplainSelectionCommandId", "guidCommandSet", "VSCodexEditorContextMenuActionsGroup");
        RequireButtonParent(vsct, "FixSelectionCommandId", "guidCommandSet", "VSCodexEditorContextMenuActionsGroup");
        RequireButtonParent(vsct, "ReviewSelectionCommandId", "guidCommandSet", "VSCodexEditorContextMenuActionsGroup");
        RequireButtonParent(vsct, "OptimizeSelectionCommandId", "guidCommandSet", "VSCodexEditorContextMenuActionsGroup");
        RequireButtonParent(vsct, "GenerateDocsCommandId", "guidCommandSet", "VSCodexEditorContextMenuActionsGroup");
        RequireButtonParent(vsct, "CreateTestFromSelectionCommandId", "guidCommandSet", "VSCodexEditorContextMenuActionsGroup");
        RequireButtonParent(vsct, "FixActiveExceptionCommandId", "guidCommandSet", "CodexDebugMenuGroup");
        RequireButtonParent(vsct, "FixActiveErrorCommandId", "guidCommandSet", "CodexErrorListContextMenuGroup");
        RequireButtonParent(vsct, "FixTestFailureCommandId", "guidCommandSet", "VSCodexTopLevelMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "VSCodexViewMenuCommandsGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "VSCodexTopLevelMenuGroup");
        RequireCommandPlacement(vsct, "OpenOptionsCommandId", "guidCommandSet", "VSCodexViewMenuCommandsGroup");
        RequireCommandPlacement(vsct, "AskCodexCommandId", "guidCommandSet", "VSCodexViewMenuCommandsGroup");
        RequireCommandPlacement(vsct, "CreateTestFromSelectionCommandId", "guidCommandSet", "VSCodexViewMenuCommandsGroup");
        RequireCommandPlacement(vsct, "FixTestFailureCommandId", "guidCommandSet", "VSCodexViewMenuCommandsGroup");
        RequireCommandPlacement(vsct, "DebugWithCodexCommandId", "guidCommandSet", "VSCodexViewMenuCommandsGroup");
        RequireCommandPlacement(vsct, "FixActiveExceptionCommandId", "guidCommandSet", "VSCodexViewMenuCommandsGroup");
        RequireCommandPlacement(vsct, "FixActiveErrorCommandId", "guidCommandSet", "VSCodexViewMenuCommandsGroup");
        RequireCommandPlacement(vsct, "CreatePlanCommandId", "guidCommandSet", "VSCodexViewMenuCommandsGroup");
        RequireCommandPlacement(vsct, "ConfigureMemoryCommandId", "guidCommandSet", "VSCodexViewMenuCommandsGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexToolsMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidSHLMainMenu", "IDG_VS_WNDO_OTRWNDWS1");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexProjectContextMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexSolutionContextMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexItemContextMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "VSCodexEditorContextMenuActionsGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexErrorListContextMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexErrorCorrectionContextMenuGroup");
        RequireNoCommandPlacement(vsct, "VSCodexEditorContextMenu", "guidCommandSet", "VSCodexEditorAllContextMenuGroup");
        RequireNoCommandPlacement(vsct, "VSCodexEditorContextMenu", "guidCommandSet", "CodexEditorContextMenuGroup");
        RequireNoCommandPlacement(vsct, "VSCodexEditorContextMenu", "guidCommandSet", "CodexProjectContextMenuGroup");
        RequireNoCommandPlacement(vsct, "VSCodexEditorContextMenu", "guidCommandSet", "CodexSolutionContextMenuGroup");
        RequireNoCommandPlacement(vsct, "VSCodexEditorContextMenu", "guidCommandSet", "CodexItemContextMenuGroup");
        RequireNoCommandPlacement(vsct, "VSCodexEditorContextMenu", "guidSHLMainMenu", "IDG_VS_CTXT_EDITOR_ALL");
        RequireNoCommandPlacement(vsct, "VSCodexEditorContextMenu", "guidSHLMainMenu", "IDG_VS_CODEWIN_DEBUG_STEP");
        RequireNoCommandPlacement(vsct, "VSCodexEditorContextMenu", "guidCommandSet", "CodexErrorCorrectionContextMenuGroup");
        RequireNoCommandPlacement(vsct, "VSCodexEditorContextMenu", "guidCommandSet", "CodexErrorListContextMenuGroup");
        RequireCommandPlacement(vsct, "AskCodexCommandId", "guidCommandSet", "CodexToolsMenuGroup");
        RequireCommandPlacement(vsct, "AskCodexCommandId", "guidCommandSet", "CodexItemContextMenuGroup");
        RequireCommandPlacement(vsct, "CreateTestFromSelectionCommandId", "guidCommandSet", "CodexToolsMenuGroup");
        RequireCommandPlacement(vsct, "CreateTestFromSelectionCommandId", "guidCommandSet", "CodexProjectContextMenuGroup");
        RequireCommandPlacement(vsct, "CreateTestFromSelectionCommandId", "guidCommandSet", "CodexSolutionContextMenuGroup");
        RequireCommandPlacement(vsct, "CreateTestFromSelectionCommandId", "guidCommandSet", "CodexItemContextMenuGroup");
        RequireCommandPlacement(vsct, "DebugWithCodexCommandId", "guidCommandSet", "CodexToolsMenuGroup");
        RequireCommandPlacement(vsct, "DebugWithCodexCommandId", "guidCommandSet", "CodexDebugMenuGroup");
        RequireCommandPlacement(vsct, "DebugWithCodexCommandId", "guidCommandSet", "CodexProjectContextMenuGroup");
        RequireCommandPlacement(vsct, "DebugWithCodexCommandId", "guidCommandSet", "CodexSolutionContextMenuGroup");
        RequireCommandPlacement(vsct, "DebugWithCodexCommandId", "guidCommandSet", "CodexItemContextMenuGroup");
        RequireCommandPlacement(vsct, "DebugWithCodexCommandId", "guidCommandSet", "CodexErrorListContextMenuGroup");
        RequireCommandPlacement(vsct, "DebugWithCodexCommandId", "guidCommandSet", "CodexErrorCorrectionContextMenuGroup");
        RequireNoCommandPlacement(vsct, "DebugWithCodexCommandId", "guidSHLMainMenu", "IDG_VS_CODEWIN_DEBUG_STEP");
        RequireCommandPlacement(vsct, "DebugWithCodexCommandId", "guidSHLMainMenu", "IDG_VS_CTXT_ERROR_CORRECTION");
        RequireCommandPlacement(vsct, "DebugWithCodexCommandId", "guidSHLMainMenu", "IDG_VS_ERRORLIST");
        RequireCommandPlacement(vsct, "FixActiveExceptionCommandId", "guidCommandSet", "VSCodexTopLevelMenuGroup");
        RequireCommandPlacement(vsct, "FixActiveExceptionCommandId", "guidCommandSet", "VSCodexEditorContextMenuActionsGroup");
        RequireCommandPlacement(vsct, "FixActiveExceptionCommandId", "guidCommandSet", "CodexErrorCorrectionContextMenuGroup");
        RequireNoCommandPlacement(vsct, "FixActiveExceptionCommandId", "guidSHLMainMenu", "IDG_VS_CODEWIN_DEBUG_STEP");
        RequireCommandPlacement(vsct, "FixActiveExceptionCommandId", "guidSHLMainMenu", "IDG_VS_CTXT_ERROR_CORRECTION");
        RequireCommandPlacement(vsct, "FixActiveErrorCommandId", "guidCommandSet", "VSCodexTopLevelMenuGroup");
        RequireCommandPlacement(vsct, "FixActiveErrorCommandId", "guidCommandSet", "VSCodexEditorContextMenuActionsGroup");
        RequireCommandPlacement(vsct, "FixActiveErrorCommandId", "guidCommandSet", "CodexToolsMenuGroup");
        RequireCommandPlacement(vsct, "FixActiveErrorCommandId", "guidCommandSet", "CodexProjectContextMenuGroup");
        RequireCommandPlacement(vsct, "FixActiveErrorCommandId", "guidCommandSet", "CodexSolutionContextMenuGroup");
        RequireCommandPlacement(vsct, "FixActiveErrorCommandId", "guidCommandSet", "CodexItemContextMenuGroup");
        RequireCommandPlacement(vsct, "FixActiveErrorCommandId", "guidCommandSet", "CodexErrorCorrectionContextMenuGroup");
        RequireCommandPlacement(vsct, "FixActiveErrorCommandId", "guidSHLMainMenu", "IDG_VS_CTXT_ERROR_CORRECTION");
        RequireCommandPlacement(vsct, "FixActiveErrorCommandId", "guidSHLMainMenu", "IDG_VS_ERRORLIST");
        RequireCommandPlacement(vsct, "FixTestFailureCommandId", "guidCommandSet", "CodexToolsMenuGroup");
        RequireCommandPlacement(vsct, "FixTestFailureCommandId", "guidCommandSet", "CodexProjectContextMenuGroup");
        RequireCommandPlacement(vsct, "FixTestFailureCommandId", "guidCommandSet", "CodexSolutionContextMenuGroup");
        RequireCommandPlacement(vsct, "FixTestFailureCommandId", "guidCommandSet", "CodexItemContextMenuGroup");
        RequireCommandPlacement(vsct, "CreatePlanCommandId", "guidCommandSet", "CodexProjectContextMenuGroup");
        RequireCommandPlacement(vsct, "CreatePlanCommandId", "guidCommandSet", "CodexSolutionContextMenuGroup");
        RequireCommandPlacement(vsct, "CreatePlanCommandId", "guidCommandSet", "CodexItemContextMenuGroup");

        RequireIdSymbol(vsct, "VSCodexTopLevelMenu");
        RequireIdSymbol(vsct, "VSCodexExtensionsMenuGroup");
        RequireIdSymbol(vsct, "VSCodexTopLevelMenuGroup");
        RequireIdSymbol(vsct, "VSCodexViewMenu");
        RequireIdSymbol(vsct, "VSCodexViewMenuCommandsGroup");
        RequireIdSymbol(vsct, "CodexViewMenuGroup");
        RequireIdSymbol(vsct, "CodexProjectContextMenuGroup");
        RequireIdSymbol(vsct, "CodexSolutionContextMenuGroup");
        RequireIdSymbol(vsct, "CodexItemContextMenuGroup");
        RequireIdSymbol(vsct, "AskCodexCommandId");
        RequireIdSymbol(vsct, "ExplainSelectionCommandId");
        RequireIdSymbol(vsct, "FixSelectionCommandId");
        RequireIdSymbol(vsct, "ReviewSelectionCommandId");
        RequireIdSymbol(vsct, "OptimizeSelectionCommandId");
        RequireIdSymbol(vsct, "GenerateDocsCommandId");
        RequireIdSymbol(vsct, "ConfigureMemoryCommandId");
        RequireIdSymbol(vsct, "FixActiveExceptionCommandId");
        RequireIdSymbol(vsct, "FixActiveErrorCommandId");
        RequireIdSymbol(vsct, "FixTestFailureCommandId");
        RequireIdSymbol(vsct, "CodexDebugMenuGroup");
        RequireIdSymbol(vsct, "CodexErrorListContextMenuGroup");
        RequireIdSymbol(vsct, "CodexErrorCorrectionContextMenuGroup");
        RequireIdSymbol(vsct, "VSCodexEditorContextMenu");
        RequireIdSymbol(vsct, "VSCodexCodeWindowContextMenuGroup");
        RequireIdSymbol(vsct, "VSCodexEditorAllContextMenuGroup");
        RequireIdSymbol(vsct, "VSCodexEditorContextMenuActionsGroup");
        RequireVisibleCommandStringsUseVSCodex(vsct);
        RequireContains(commandSource, "OleMenuCommand", "VSCodex context commands must participate in Visual Studio query-status routing.");
        RequireContains(commandSource, "BeforeQueryStatus", "VSCodex context commands must update visibility and debug labels when menus open.");
        RequireContains(commandSource, "FindCommand(commandIdentifier)", "VSCodex command registration must tolerate retries without duplicate command failures.");
        RequireContains(commandSource, "for (var attempt = 1; attempt <= 10", "VSCodex command registration must retry until IMenuCommandService is available.");
        RequireContains(commandSource, "IMenuCommandService was unavailable", "Command registration failures must be logged instead of silently leaving visible menu items without handlers.");
        RequireMatches(commandSource, @"AddCommand\s*\(\s*commandService\s*,\s*CodexCommandIds\.OpenToolWindowCommandId\s*,\s*ExecuteOpenToolWindow\s*,\s*QueryOpenToolWindowCommandStatus\s*\)", "The Open VSCodex Tool Window command must have its own query-status handler so it remains visible after the tool window is closed.");
        RequireMatches(commandSource, @"QueryOpenToolWindowCommandStatus[\s\S]*?command\.Visible\s*=\s*true\s*;[\s\S]*?command\.Enabled\s*=\s*true\s*;[\s\S]*?command\.Text\s*=\s*""VSCodex""\s*;", "The Open VSCodex command must stay visible, enabled, and named VSCodex.");
        RequireContains(commandSource, "QueryEditorContextCommandStatus", "Editor selection actions must be query-status aware.");
        RequireContains(commandSource, "QueryDebugCommandStatus", "Debug With VSCodex must adapt to runtime exception break mode.");
        RequireContains(commandSource, "Debug Exception with VSCodex", "Runtime exception break mode must show a specific VSCodex debug command label.");
        RequireContains(commandSource, "QueryActiveExceptionCommandStatus", "Active exception fixes must be query-status aware.");
        RequireContains(commandSource, "QueryActiveErrorCommandStatus", "Error-list fixes must be query-status aware.");
        RequireContains(commandSource, "QueryTestFailureCommandStatus", "Test failure fixes must be query-status aware.");
        RequireContains(commandSource, "Fix Active Exception with VSCodex", "Active exception context must show a specific VSCodex fix label.");
        RequireContains(commandSource, "Fix with VSCodex", "Error and vulnerability contexts must show the expected VSCodex fix label.");
        RequireContains(commandSource, "Fix Test Failure with VSCodex", "Test failure context must show a specific VSCodex fix label.");
        RequireContains(commandSource, "BuildTestFailurePrompt", "Test failure commands must use a dedicated test failure prompt.");
        RequireContains(assistantSource, "BuildTestFailurePrompt", "Coding assistant context must know how to build a test failure prompt.");
        RequireContains(assistantSource, "Fix the active Visual Studio test failure", "Test failure prompt must be scoped to Visual Studio test-failure assistance.");
        RequireContains(packageSource, "VisualStudioMenuIntegrationService.InitializeAsync(this)", "Package load must install visible VSCodex menu entries even if command-cache placement is stale.");
        RequireContains(packageSource, "[ProvideMenuResource(\"Menus.ctmenu\", 5)]", "The menu resource version must be bumped when VSCT placements change so Visual Studio refreshes the cached command table.");
        RequireContains(menuBridge, "View.VSCodex", "Runtime menu bridge must expose the VSCodex tool window in the Visual Studio View menu.");
        RequireContains(menuBridge, "var viewPopup = viewCommandBar == null ? null : EnsurePopup(viewCommandBar, \"VSCodex Actions\")", "Runtime menu bridge must create a visible View > VSCodex Actions submenu without conflicting with the direct View > VSCodex command.");
        RequireContains(menuBridge, "FindControlByCaption(commandBar, command.Caption)", "Runtime menu bridge must detect existing static menu controls before adding replacements.");
        RequireContains(menuBridge, "continue;", "Runtime menu bridge must keep existing static menu controls instead of deleting them.");
        RequireDoesNotContain(menuBridge, "DeleteControl", "Runtime menu bridge must not delete static VSCT controls while repairing menu placement.");
        RequireContains(menuBridge, "for (var attempt = 1; attempt <= 10", "Runtime menu bridge must retry until the command table is available.");
        RequireContains(menuBridge, "return openCommandVisible;", "Runtime menu bridge must not mark itself installed unless View > VSCodex is visible.");
        RequireContains(menuBridge, "CodexCommandIds.CommandSetGuidString", "Runtime menu bridge must fall back to GUID/ID command lookup when VS2026 does not expose canonical DTE names.");
        RequireContains(menuBridge, "public int CommandId", "Runtime menu bridge command specs must carry command IDs for VS2026 command lookup.");
        RequireContains(menuBridge, "var otherWindowsPopup = viewCommandBar == null ? null : FindControlByCaption(viewCommandBar, \"Other Windows\")", "Runtime menu bridge must also target the canonical View > Other Windows recovery surface.");
        RequireContains(menuBridge, "SetVisible", "Runtime menu bridge must force-added menu controls to be visible.");
        RequireContains(menuBridge, "SetVisible(existing);", "Runtime menu bridge must force existing VSCodex menu popups visible, not only newly-created popups.");
        RequireContains(menuBridge, "Context menus are declared through VSCT only", "Runtime menu repair must not add duplicate editor context menu popups.");
        RequireContains(menuBridge, "Fix with VSCodex", "Runtime menu bridge must expose the requested vulnerability/error fix action.");
        RequireDoesNotContain(menuBridge, "Code Window", "Runtime menu bridge must not inject editor context menu popups.");
        RequireDoesNotContain(menuBridge, "Text Editor", "Runtime menu bridge must not inject editor context menu popups.");
        RequireDoesNotContain(menuBridge, "SolutionExplorerContextBars", "Runtime menu bridge must not duplicate Solution Explorer context menus.");
    }

    [Test]
    public void Codex_defaults_include_failover_budget_analytics_and_ReactiveMemory_hooks()
    {
        var models = ReadText("src/VSCodex/Models/CodexModels.cs");
        var promptBuilder = ReadText("src/VSCodex/Services/PromptBuilder.cs");
        var mcpConfig = ReadText("src/VSCodex/Services/McpConfigService.cs");
        var mcpTools = ReadText("src/VSCodex/Services/McpToolCatalogService.cs");
        var reactiveMemory = ReadText("src/VSCodex/Services/ReactiveMemoryService.cs");
        var solutionMonitor = ReadText("src/VSCodex/Services/SolutionLoadMonitorService.cs");
        var appBuilder = ReadText("src/VSCodex/Infrastructure/RxAppBuilder.cs");
        var viewModel = ReadText("src/VSCodex/ViewModels/VSCodexToolWindowViewModel.cs");
        var view = ReadText("src/VSCodex/Views/VSCodexToolWindowControl.xaml");
        var analytics = ReadText("src/VSCodex/Services/ModelAnalyticsService.cs");

        RequireContains(models, "DefaultFailoverModel", "Settings must expose a failover model.");
        RequireContains(models, "CodexAccessLevel", "Settings must expose a friendly access level abstraction over sandbox modes.");
        RequireContains(models, "EnabledSkillPaths", "Settings must persist enabled skill selections across refreshes.");
        RequireContains(models, "ContextRemainingPercent", "Model analytics must expose remaining context percentage.");
        RequireContains(models, "gpt-5.5", "Primary model defaults must include the current flagship coding model.");
        RequireContains(models, "gpt-5.4-mini", "Budget defaults must include a cheaper model option.");
        RequireContains(promptBuilder, "reactivememory_status", "Prompt builder must inject ReactiveMemory session-start hooks.");
        RequireContains(promptBuilder, "reactivememory_react_to_prompt", "Prompt builder must inject per-prompt ReactiveMemory hooks.");
        RequireContains(promptBuilder, "Recovered ReactiveMemory context", "Prompt builder must inject recovered ReactiveMemory output into the Codex prompt.");
        RequireContains(mcpConfig, "PreferredReactiveMemoryServerName = \"cp-reactivememory-mcp-server\"", "MCP config service must install ReactiveMemory under the Codex-shared server name.");
        RequireContains(mcpConfig, "MigrateLegacyReactiveMemoryBlock", "MCP config service must migrate the older VSCodex ReactiveMemory fallback instead of keeping duplicate memory servers active.");
        RequireContains(mcpConfig, "FindMcpServerBlock", "MCP config service must validate the real ReactiveMemory server block instead of matching unrelated text.");
        RequireContains(mcpConfig, "enabled\\s*=\\s*false", "MCP config service must re-enable the default ReactiveMemory server when it is explicitly disabled.");
        RequireContains(mcpConfig, "void Save(IEnumerable<McpServerDefinition> servers)", "MCP config service must persist user-managed MCP server edits.");
        RequireContains(mcpConfig, "CreateTemplate(string transportType)", "MCP config service must create stdio and URL MCP server drafts from the UI.");
        RequireContains(mcpConfig, "CP.ReactiveMemory.Mcp.Server", "MCP config service must know the ReactiveMemory package identity.");
        RequireContains(mcpConfig, "CP.ReactiveMemory.MCP.Server.csproj", "MCP config service must find the current ReactiveMemory server project name when using a local checkout.");
        RequireContains(mcpTools, "InvokeToolAsync", "MCP tooling must expose runtime tool invocation, not only tool discovery.");
        RequireContains(mcpTools, "tools/call", "MCP tooling must call MCP tools through the JSON-RPC tools/call method.");
        RequireContains(mcpTools, "ResolveCommandPath", "MCP tooling must resolve command names before starting stdio servers from Visual Studio.");
        RequireContains(mcpTools, "ResolveDotNetPath", "MCP tooling must launch dnx through dotnet.exe instead of leaving a PowerShell wrapper alive.");
        RequireContains(mcpTools, "\"dnx\" + BuildArgumentSuffix(args)", "MCP tooling must preserve dnx server arguments when bypassing dnx.ps1.");
        RequireContains(mcpTools, "\".ps1\"", "MCP tooling must support dnx.ps1-backed MCP servers.");
        RequireContains(mcpTools, "Path.Combine(root!, \"dotnet\", \"dotnet.exe\")", "MCP tooling must find dotnet.exe even when Visual Studio starts without dotnet on PATH.");
        RequireContains(mcpTools, "-NoProfile -ExecutionPolicy Bypass -File", "MCP tooling must launch PowerShell-backed MCP commands without relying on shell execution.");
        RequireContains(mcpTools, "ProbeTimeoutSeconds = 20", "MCP discovery must allow dnx-based servers enough cold-start time.");
        RequireContains(mcpTools, "_cache[server.Name] = tools;", "MCP discovery must cache real tool lists.");
        RequireDoesNotContain(mcpTools, "_cache[server.Name] = new[]", "MCP discovery must not cache synthetic fallback tools after a failed probe.");
        RequireContains(reactiveMemory, "IReactiveMemoryService", "VSCodex must have a runtime ReactiveMemory service.");
        RequireContains(reactiveMemory, "ReactToPromptAsync", "ReactiveMemory must be called before requests so context can be restored.");
        RequireContains(reactiveMemory, "WriteDiaryAsync", "ReactiveMemory must write durable memories after requests complete.");
        RequireContains(reactiveMemory, "AddMemoryAsync", "Explicit memory commands must persist through ReactiveMemory.");
        RequireContains(reactiveMemory, "reactivememory_check_duplicate", "Explicit memory saves should check for duplicates before adding durable memories.");
        RequireContains(reactiveMemory, "InvokeToolAsync(server, toolName, arguments)", "ReactiveMemory must use real MCP tool calls.");
        RequireContains(reactiveMemory, "reactivememory_add_drawer", "ReactiveMemory ProjectMiner fallback must call the known drawer filing tool when discovery cannot list tools.");
        RequireContains(reactiveMemory, "ScoreReactiveMemoryServer", "ReactiveMemory must pick the most capable configured MCP server instead of the first name match.");
        RequireContains(reactiveMemory, "cp-reactivememory-mcp-server", "ReactiveMemory should prefer the dnx package server already configured for Codex.");
        RequireContains(reactiveMemory, "CP.ReactiveMemory.Mcp.Server@", "ReactiveMemory should prefer versioned dnx package entries over stale fallback blocks.");
        RequireContains(reactiveMemory, "if (!automatic && projectMinerTool != null)", "Automatic startup scans must not invoke unbounded ProjectMiner tools.");
        RequireContains(solutionMonitor, "BuildWorkspaceIdentityFromSolutionPath", "Startup ProjectMiner scans must build a background-safe solution identity from the captured solution path.");
        RequireContains(solutionMonitor, "Task.Run(async () =>", "Startup ProjectMiner scans must run from a background worker.");
        RequireDoesNotContain(solutionMonitor, "_workspace.RefreshWorkspaceIdentity", "Startup ProjectMiner scans must not refresh DTE workspace state on the UI thread.");
        RequireDoesNotContain(solutionMonitor, "await _joinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);", "Queued ProjectMiner scans must not switch back to the Visual Studio UI thread.");
        RequireContains(ReadText("src/VSCodex/Services/CodingAssistantContextService.cs"), "Current solution context:", "ReactiveMemory setup prompts must include the active solution/workspace context instead of generic setup text.");
        RequireContains(ReadText("src/VSCodex/Services/CodingAssistantContextService.cs"), "_workspace.RefreshWorkspaceIdentity();", "ReactiveMemory setup prompts must capture the currently loaded solution before building the prompt.");
        RequireContains(appBuilder, "RegisterSingleton<IReactiveMemoryService>", "ReactiveMemory service must be registered for the tool-window view model.");
        RequireContains(viewModel, "_reactiveMemory.ReactToPromptAsync", "Run must update ReactiveMemory context before calling Codex.");
        RequireContains(viewModel, "ReactiveMemoryContext = memoryReaction.ContextText", "Run must pass recovered ReactiveMemory context into the Codex request.");
        RequireContains(viewModel, "_reactiveMemory.WriteDiaryAsync", "Run completion must write a ReactiveMemory diary entry.");
        RequireContains(viewModel, "_reactiveMemory.AddMemoryAsync", "Memory buttons must persist through ReactiveMemory.");
        RequireContains(viewModel, "AddMcpStdioServerCommand", "Tool window must expose an Add stdio MCP server command.");
        RequireContains(viewModel, "AddMcpUrlServerCommand", "Tool window must expose an Add URL MCP server command.");
        RequireContains(viewModel, "SaveMcpServersCommand", "Tool window must persist MCP server edits back to Codex config.");
        RequireContains(viewModel, "CreateSkillCommand", "Tool window must let developers create local Codex skills.");
        RequireContains(viewModel, "SaveSkillsCommand", "Tool window must persist enabled skill selections.");
        RequireContains(viewModel, "AccessLevelFromSandbox", "Tool window must map friendly access levels to Codex sandbox modes.");
        RequireContains(viewModel, "ContextRemainingSummary", "Tool window must expose context-size and remaining-context summary text.");
        RequireContains(view, "Header=\"Analytics\"", "Tool window must expose model/cost analytics.");
        RequireContains(view, "Header=\"History\"", "The obsolete in-window settings tab must be replaced by conversation history.");
        RequireContains(view, "Add stdio", "Tool window must expose add-MCP server controls.");
        RequireContains(view, "Create skill", "Tool window must expose skill creation controls.");
        RequireContains(view, "ContextRemainingSummary", "Tool window must show remaining context percentage.");
        RequireDoesNotContain(view, "Header=\"Settings\"", "Model and execution settings must live under Tools > Options instead of the tool window.");
        RequireContains(analytics, "EstimatedSavingsPercent", "Analytics must estimate whether a cheaper model can be used.");
        RequireContains(analytics, "ContextRemainingTokens", "Analytics must estimate context remaining tokens.");
    }

    [Test]
    public void Package_auto_loads_and_defers_first_run_tool_window_creation_until_after_initialization()
    {
        var packageSource = ReadText("src/VSCodex/VSCodexPackage.cs");
        var commandSource = ReadText("src/VSCodex/Commands/OpenVSCodexToolWindowCommand.cs");

        RequireContains(packageSource, "ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string", "Package must auto-load before a solution is open.");
        RequireContains(packageSource, "ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string", "Package must auto-load when a solution exists.");
        RequireContains(packageSource, "OpenVSCodexToolWindowCommand.InitializeAsync(this)", "Package initialization must register VSCodex commands.");
        RequireContains(packageSource, "ScheduleReactiveMemoryProjectMinerInitialization();", "ReactiveMemory startup monitoring must be scheduled after command registration.");
        RequireDoesNotContain(packageSource, "await InitializeReactiveMemoryProjectMinerAsync(cancellationToken)", "Package initialization must not synchronously wait for ProjectMiner monitoring setup.");
        RequireContains(packageSource, "ScheduleShowToolWindowOnFirstLaunch();", "First-run tool-window launch must be scheduled after command registration.");
        RequireContains(packageSource, "JoinableTaskFactory.RunAsync", "First-run tool-window launch must not block package initialization.");
        RequireContains(packageSource, "Task.Delay(TimeSpan.FromMilliseconds(1500)", "First-run tool-window launch must wait for the shell to settle.");
        RequireContains(packageSource, "FirstLaunchToolWindowOpened", "First-run state must be persisted so user layout is respected thereafter.");
        RequireContains(packageSource, "FirstLaunchToolWindowOpenedV8", "The first-run key must advance when the tool-window launch behavior changes.");
        RequireContains(packageSource, "FirstLaunchToolWindowOpenedV7", "First-run tool-window creation must respect older first-run markers after extension updates.");
        RequireContains(packageSource, "ShowToolWindowAsync(typeof(VSCodexToolWindowPane)", "The package must still show the VSCodex tool window on first run.");
        RequireDoesNotContain(packageSource, "await ShowToolWindowOnFirstLaunchAsync(cancellationToken)", "Package initialization must not synchronously await WPF tool-window creation.");
        RequireContains(commandSource, "ShowToolWindowAsync(typeof(VSCodexToolWindowPane)", "The VSCodex tool window must still open through the explicit command path.");
    }

    [Test]
    public void Tool_window_startup_failures_leave_a_visible_VSCodex_diagnostic_surface()
    {
        var toolWindow = ReadText("src/VSCodex/ToolWindows/VSCodexToolWindowPane.cs");
        var fallback = ReadText("src/VSCodex/Controls/VSCodexToolWindowFallbackControl.cs");
        var commandSource = ReadText("src/VSCodex/Commands/OpenVSCodexToolWindowCommand.cs");

        RequireContains(toolWindow, "try", "Tool-window content creation must be guarded so startup failures do not make VSCodex disappear.");
        RequireContains(toolWindow, "ActivityLog.TryLogError", "Tool-window startup failures must be written to the Visual Studio ActivityLog.");
        RequireContains(toolWindow, "VSCodexToolWindowFallbackControl", "A visible diagnostic fallback control must be shown when the main UI cannot be created.");
        RequireContains(fallback, "VSCodex could not initialize", "The fallback surface must identify itself as a VSCodex initialization failure.");
        RequireContains(fallback, "EnvironmentColors.ToolWindowBackgroundBrushKey", "The fallback surface must still use Visual Studio theme resources.");
        RequireContains(commandSource, "RunVSCodexCommand", "Menu command execution must centralize error handling.");
        RequireContains(commandSource, "VsShellUtilities.ShowMessageBox", "Explicit VSCodex command failures must show a visible Visual Studio message.");
    }

    [Test]
    public void VSCodex_settings_are_hosted_in_Tools_Options_with_a_modern_theme_aware_page()
    {
        var packageSource = ReadText("src/VSCodex/VSCodexPackage.cs");
        var commandSource = ReadText("src/VSCodex/Commands/OpenVSCodexToolWindowCommand.cs");
        var optionsPage = ReadText("src/VSCodex/Options/OptionsProvider.cs");
        var project = ReadText("src/VSCodex/VSCodex.csproj");
        var settingsStore = ReadText("src/VSCodex/Services/SettingsStore.cs");
        var viewModel = ReadText("src/VSCodex/ViewModels/VSCodexToolWindowViewModel.cs");
        var manifest = ReadText("src/VSCodex/source.extension.vsixmanifest");

        RequireContains(packageSource, "ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), \"VSCodex\", \"General\"", "VSCodex settings must be registered under Tools > Options > VSCodex with the modern Community options provider.");
        RequireContains(packageSource, "ProvideProfile(typeof(OptionsProvider.GeneralOptions), \"VSCodex\", \"General\"", "VSCodex settings should participate in Visual Studio profile import/export through the modern options provider.");
        RequireContains(commandSource, "ShowOptionPage(typeof(OptionsProvider.GeneralOptions))", "The VSCodex Settings menu command must open the modern Tools > Options provider instead of the tool window.");
        RequireDoesNotContain(commandSource, "OpenToolWindowAsync(window => window.ShowSettings())", "Settings must not be forced into the docked tool-window settings tab.");
        RequireContains(project, "Community.VisualStudio.Toolkit.17", "Modern Visual Studio settings should use the Community Toolkit Options Page pattern documented by Microsoft.");
        RequireContains(optionsPage, "BaseOptionPage<VSCodexOptionsModel>", "The options page must use the Community Toolkit modern options provider instead of a hand-built legacy UIElementDialogPage.");
        RequireContains(optionsPage, "[ComVisible(true)]", "The Community Toolkit DialogPage implementation must remain visible to COM for Visual Studio registration.");
        RequireContains(optionsPage, "BaseOptionModel<VSCodexOptionsModel>", "The options model must use the Community Toolkit BaseOptionModel pattern.");
        RequireDoesNotContain(optionsPage, "UIElementDialogPage", "Legacy UIElementDialogPage settings must not be used for VSCodex general settings.");
        RequireContains(optionsPage, "Category(\"Runtime\")", "The modern settings page must group runtime settings.");
        RequireContains(optionsPage, "Category(\"Models\")", "The modern settings page must group model settings.");
        RequireContains(optionsPage, "Category(\"Approvals and sandbox\")", "The modern settings page must group approval and sandbox settings.");
        RequireContains(optionsPage, "Category(\"Agents\")", "The modern settings page must group agent settings.");
        RequireContains(optionsPage, "Category(\"Context, skills, and memory\")", "The modern settings page must group context, skill, and memory settings.");
        RequireContains(optionsPage, "LoadFromSettingsStore", "The modern options model must hydrate from the shared VSCodex settings store.");
        RequireContains(optionsPage, "SaveToSettingsStore", "The modern options model must persist through the shared VSCodex settings store.");
        RequireContains(optionsPage, "store.Save(settings)", "Saving modern options must broadcast settings to open VSCodex tool windows.");
        RequireContains(optionsPage, "DefaultModel", "The options page must configure the primary model.");
        RequireContains(optionsPage, "DefaultFailoverModel", "The options page must configure model failover.");
        RequireMatches(optionsPage, @"\[TypeConverter\(typeof\(AvailableModelTypeConverter\)\)\]\s*public string DefaultModel", "The default model option must render as a model dropdown.");
        RequireMatches(optionsPage, @"\[TypeConverter\(typeof\(AvailableModelTypeConverter\)\)\]\s*public string DefaultFailoverModel", "The failover model option must render as a model dropdown.");
        RequireMatches(optionsPage, @"\[TypeConverter\(typeof\(AvailableModelTypeConverter\)\)\]\s*public string DefaultOrchestrationModel", "The orchestration model option must render as a model dropdown.");
        RequireMatches(optionsPage, @"\[TypeConverter\(typeof\(AvailableModelTypeConverter\)\)\]\s*public string DefaultBudgetModel", "The budget model option must render as a model dropdown.");
        RequireMatches(optionsPage, @"\[TypeConverter\(typeof\(ReasoningEffortTypeConverter\)\)\]\s*public string DefaultReasoningEffort", "Reasoning effort must render as a dropdown.");
        RequireMatches(optionsPage, @"\[TypeConverter\(typeof\(VerbosityTypeConverter\)\)\]\s*public string DefaultVerbosity", "Verbosity must render as a dropdown.");
        RequireContains(optionsPage, "public sealed class AvailableModelTypeConverter : StringConverter", "The options page must provide model standard values for dropdown selection.");
        RequireContains(optionsPage, "GetStandardValuesSupported", "String options must advertise standard values so Visual Studio shows a combobox.");
        RequireContains(optionsPage, "GetStandardValuesExclusive", "String options must restrict selection to the offered combobox values.");
        RequireContains(optionsPage, "settings.CustomModels", "Model dropdown values must come from the available model list.");
        RequireContains(optionsPage, "RefreshProperties(RefreshProperties.All)", "Changing custom models must refresh model dropdown choices.");
        RequireContains(optionsPage, "DefaultUseMultiAgentOrchestration", "The options page must configure multi-agent orchestration.");
        RequireContains(optionsPage, "CP.ReactiveMemory.Mcp.Server", "The options page must surface the default ReactiveMemory MCP server identity.");
        RequireContains(optionsPage, "SaveSettingsToStorage", "The options page must persist settings through the existing VSCodex settings store.");
        RequireContains(settingsStore, "static BehaviorSubject<ExtensionSettings>? SharedSettings", "Tools > Options and the tool window must share one live settings stream.");
        RequireContains(settingsStore, "SettingsChanged => _settings.AsObservable()", "Runtime services must observe Tools > Options changes through the shared settings stream.");
        RequireContains(settingsStore, "Normalize(settings);", "Saved Tools > Options values must be normalized before they are persisted and broadcast.");
        RequireContains(settingsStore, "NormalizeAgentRoles", "Persisted agent roles must be de-duplicated before the Agents tool pane is populated.");
        RequireContains(settingsStore, "Store.Write(path, settings);", "Workspace settings loaded from disk must be rewritten after normalization so duplicate agent roles are repaired.");
        RequireContains(viewModel, "_settingsStore.SettingsChanged.ObserveOnSafe(_uiScheduler).Subscribe(ApplySettingsFromStore)", "An open VSCodex tool window must react to Tools > Options changes without restarting Visual Studio.");
        RequireContains(viewModel, "ApplySettingsFromStore", "Tools > Options changes must update the live tool-window run settings.");
        RequireContains(manifest, "<DisplayName>VSCodex</DisplayName>", "The VSIX display name must be VSCodex.");
        RequireContains(manifest, "Version=\"0.4.2\"", "The VSIX version must change so Visual Studio updates the installed experimental extension.");
        RequireContains(manifest, "Version=\"[4.8,)\"", "Classic in-process VSCodex VSIX packages must target the .NET Framework runtime Visual Studio 2022 runs on.");
    }

    [Test]
    public void Marketplace_icon_documentation_and_publish_workflow_are_packaged()
    {
        var manifest = ReadText("src/VSCodex/source.extension.vsixmanifest");
        var project = ReadText("src/VSCodex/VSCodex.csproj");
        var view = ReadText("src/VSCodex/Views/VSCodexToolWindowControl.xaml");
        var readme = ReadText("README.md");
        var publishManifest = ReadText("marketplace/vs-publish.json");
        var workflow = ReadText(".github/workflows/publish-vsix.yml");

        RequireExists("src/VSCodex/Resources/VSCodexIcon.svg", "The source icon artwork must be tracked.");
        RequireExists("src/VSCodex/Resources/VSCodexIcon-32.png", "The tool-window icon asset must be tracked.");
        RequireExists("src/VSCodex/Resources/VSCodexIcon-128.png", "The Marketplace icon asset must be tracked.");
        RequireExists("src/VSCodex/Resources/VSCodexIcon-256.png", "The Marketplace preview image asset must be tracked.");
        RequireContains(manifest, "<Icon>Resources\\VSCodexIcon-128.png</Icon>", "The VSIX manifest must expose the VSCodex Marketplace icon.");
        RequireContains(manifest, "<PreviewImage>Resources\\VSCodexIcon-256.png</PreviewImage>", "The VSIX manifest must expose the VSCodex preview image.");
        RequireContains(project, "<Resource Include=\"Resources\\VSCodexIcon-32.png\" />", "The tool-window header icon must be available as a WPF resource.");
        RequireContains(project, "<Content Include=\"Resources\\VSCodexIcon-128.png\">", "The Marketplace icon must be included in the VSIX.");
        RequireContains(project, "<Content Include=\"Resources\\VSCodexIcon-256.png\">", "The Marketplace preview image must be included in the VSIX.");
        RequireContains(view, "VSCodexIcon-32.png", "The VSCodex tool window must show the VSCodex icon in its header.");

        RequireContains(readme, "# VSCodex", "README must be product documentation for VSCodex.");
        RequireContains(readme, "## Getting Started", "README must document first-run setup.");
        RequireContains(readme, "## Main Tool Window", "README must document the tool-window experience.");
        RequireContains(readme, "## Editor and Debug Menus", "README must document context-menu and debug hooks.");
        RequireContains(readme, "## MCP Servers", "README must document MCP server control.");
        RequireContains(readme, "## Marketplace Publishing", "README must document Marketplace publishing.");

        RequireContains(publishManifest, "\"$schema\": \"http://json.schemastore.org/vsix-publish\"", "Marketplace publishing must use the supported VSIX publish manifest schema.");
        RequireContains(publishManifest, "\"overview\": \"README.md\"", "The Marketplace overview must use README.md.");
        RequireContains(publishManifest, "\"internalName\": \"VSCodex\"", "The Marketplace internal name must be stable.");
        RequireContains(workflow, "microsoft/setup-msbuild@v3", "The Marketplace workflow must build with MSBuild on Windows.");
        RequireContains(workflow, "actions/upload-artifact@v7", "The Marketplace workflow must publish the built VSIX as an artifact.");
        RequireContains(workflow, "Install-VSCodex.cmd", "The Marketplace workflow artifact must include the Release installer command launcher.");
        RequireContains(workflow, "Install-VSCodex.ps1", "The Marketplace workflow artifact must include the Release installer PowerShell launcher.");
        RequireContains(workflow, "VSCodex-vsix-installer", "The Marketplace workflow artifact must package the VSIX and launcher files together.");
        RequireContains(workflow, "VsixPublisher.exe", "The Marketplace workflow must publish through the supported Visual Studio Marketplace CLI.");
        RequireContains(workflow, "VS_MARKETPLACE_PAT", "The Marketplace workflow must authenticate with a secret PAT.");
        RequireContains(workflow, "marketplace/vs-publish.json", "The Marketplace workflow must use the repository publish manifest.");
    }

    [Test]
    public void Prompt_context_updates_are_marshalled_to_the_Visual_Studio_UI_thread()
    {
        var viewModel = ReadText("src/VSCodex/ViewModels/VSCodexToolWindowViewModel.cs");
        var workspaceService = ReadText("src/VSCodex/Services/WorkspaceContextService.cs");
        var assistantContext = ReadText("src/VSCodex/Services/CodingAssistantContextService.cs");

        RequireContains(workspaceService, "SearchContextReferences(string query, int limit)", "Workspace service must expose context reference search.");
        RequireContains(workspaceService, "ThreadHelper.ThrowIfNotOnUIThread();", "DTE-backed workspace calls must keep their UI-thread guard.");
        RequireContains(workspaceService, "maxChars > 0 && selectedText.Length > maxChars", "Explicit VSCodex selection actions must be able to include selected code of any length.");
        RequireContains(workspaceService, "MaxIndexedFiles", "Repository file suggestions must use a bounded index instead of repeatedly walking the full tree while typing.");
        RequireContains(workspaceService, "SearchExplicitPath", "Typing @ with an absolute or rooted disk path must resolve files outside the repository.");
        RequireContains(workspaceService, "Regex.Matches", "Reference parsing must support quoted @ file paths that contain spaces.");
        RequireContains(workspaceService, "FormatReferenceKey('@'", "Repository and disk file references must be inserted as resolvable @ tokens.");
        RequireContains(assistantContext, "GetCurrentSelectionReference(0)", "Context-menu selection prompts must request the full selected code, not a truncated preview.");
        RequireContains(viewModel, "JoinableTaskFactory joinableTaskFactory", "The tool-window view model must receive the Visual Studio joinable task factory.");
        RequireContains(viewModel, "_joinableTaskFactory.RunAsync", "Prompt changes must marshal throttled work through the Visual Studio joinable task factory.");
        RequireContains(viewModel, "await _joinableTaskFactory.SwitchToMainThreadAsync();", "Prompt context updates must switch to the Visual Studio UI thread before reading DTE state.");
        RequireContains(viewModel, "Volatile.Read(ref _promptChangeRevision)", "Stale throttled prompt updates must be ignored after marshaling to the UI thread.");
        RequireContains(viewModel, "UpdateReferenceSuggestions(prompt);", "Reference suggestions must still be refreshed after the UI-thread switch.");
        RequireContains(viewModel, "UpdatePromptSuggestions(prompt);", "Prompt typing must refresh inline @/#// suggestion popups.");
    }

    [Test]
    public void Codex_transport_avoids_Visual_Studio_Newtonsoft_binding_breaks_and_reports_missing_executables()
    {
        var sdkClient = ReadText("src/VSCodex/Services/CodexSdkJsonClient.cs");
        var cliClient = ReadText("src/VSCodex/Services/CodexCliClient.cs");
        var orchestrator = ReadText("src/VSCodex/Services/CodexOrchestrator.cs");

        RequireContains(sdkClient, "ToCompactJson", "SDK bridge JSON must use a helper that avoids Visual Studio binding-sensitive Newtonsoft overloads.");
        RequireContains(sdkClient, "JsonConvert.SerializeObject(token)", "SDK bridge JSON serialization must not require JToken.ToString(Formatting).");
        RequireDoesNotContain(sdkClient, "Formatting.None", "SDK bridge JSON must avoid JToken.ToString(Formatting.None), which can bind to older Visual Studio Newtonsoft.Json assemblies.");
        RequireContains(sdkClient, "Node.js executable was not found", "Missing node.exe must produce an actionable VSCodex error.");
        RequireContains(sdkClient, "winget install OpenJS.NodeJS.LTS", "Missing node.exe guidance must include Windows install instructions.");
        RequireContains(cliClient, "Codex CLI executable was not found", "Missing codex.exe must produce an actionable VSCodex error.");
        RequireContains(cliClient, "npm install -g @openai/codex", "Missing Codex CLI guidance must include the Windows npm install command.");
        RequireContains(cliClient, "Win32Exception", "Process start failures must be translated from raw Win32 exceptions.");
        RequireContains(cliClient, "--config ", "Codex CLI fallback must use current codex exec config overrides.");
        RequireContains(cliClient, "approval_policy=", "Codex CLI fallback must pass approval policy through --config, not the removed --approval-policy flag.");
        RequireContains(cliClient, "--cd ", "Codex CLI fallback must run against the Visual Studio workspace root.");
        RequireContains(cliClient, "--skip-git-repo-check", "Codex CLI fallback must support non-git Visual Studio solution folders in non-interactive mode.");
        RequireContains(cliClient, "redirectStandardInput: true", "Codex CLI fallback must pass large enriched prompts through stdin instead of the command line.");
        RequireContains(cliClient, "StandardInput.WriteAsync", "Codex CLI fallback must write the enriched VSCodex prompt to codex exec stdin.");
        RequireContains(cliClient, "_active.ExitCode != 0", "Codex CLI fallback must not report success when codex exec exits with an error.");
        RequireContains(cliClient, "ShouldPassProfile", "Codex CLI fallback must centralize profile validation.");
        RequireContains(cliClient, "!profile.Equals(\"default\"", "Codex CLI fallback must not pass the implicit default profile when it is not configured.");
        RequireContains(cliClient, "IOException", "Codex CLI fallback must handle early process exits without replacing the useful codex error with a pipe error.");
        RequireContains(cliClient, "stdinException", "Codex CLI fallback must preserve stdin failures as secondary diagnostics.");
        RequireContains(cliClient, "IsProcessTerminationNoise", "Codex CLI fallback must filter Windows taskkill success lines from user-visible output.");
        RequireContains(cliClient, "SUCCESS: The process with PID", "Codex CLI fallback must recognize the Windows taskkill stdout line that can contaminate codex output.");
        RequireDoesNotContain(cliClient, "--approval-policy", "Codex CLI fallback must not use the removed codex exec --approval-policy flag.");
        RequireContains(sdkClient, "GetRateLimitsAsync", "SDK transport must expose a rate-limit telemetry call that can run on tool-window load.");
        RequireContains(orchestrator, "GetRateLimitsAsync", "The orchestrator must expose rate-limit telemetry independently from request execution.");
        RequireContains(orchestrator, "SDK failure:", "CLI fallback failures must preserve the original SDK bridge failure.");
        RequireContains(orchestrator, "npm install -g @openai/codex-sdk", "Combined run failure must direct the user to install the Codex SDK.");
        RequireContains(orchestrator, "%USERPROFILE%\\\\.codex\\\\config.toml", "Combined run failure must point users to the Codex profile configuration when profile selection breaks fallback.");
    }

    [Test]
    public void Workspace_execution_uses_repository_root_and_project_scoped_identity()
    {
        var workspace = ReadText("src/VSCodex/Services/WorkspaceContextService.cs");
        var models = ReadText("src/VSCodex/Models/CodexModels.cs");
        var viewModel = ReadText("src/VSCodex/ViewModels/VSCodexToolWindowViewModel.cs");
        var promptBuilder = ReadText("src/VSCodex/Services/PromptBuilder.cs");
        var sdkClient = ReadText("src/VSCodex/Services/CodexSdkJsonClient.cs");
        var cliClient = ReadText("src/VSCodex/Services/CodexCliClient.cs");
        var bridge = ReadText("src/VSCodex/Resources/codex-bridge.mjs");
        var orchestrator = ReadText("src/VSCodex/Services/CodexOrchestrator.cs");
        var orchestration = ReadText("src/VSCodex/Services/TaskOrchestrationService.cs");
        var memoryStore = ReadText("src/VSCodex/Services/MemoryStore.cs");
        var localPaths = ReadText("src/VSCodex/Infrastructure/LocalPaths.cs");
        var reactiveMemory = ReadText("src/VSCodex/Services/ReactiveMemoryService.cs");
        var solutionMonitor = ReadText("src/VSCodex/Services/SolutionLoadMonitorService.cs");
        var package = ReadText("src/VSCodex/VSCodexPackage.cs");

        RequireContains(workspace, "ResolveWorkspaceStartDirectory", "Workspace discovery must start from the loaded solution and fall back to the active project.");
        RequireContains(workspace, "GetOpenFolderDirectory", "Workspace discovery must support Visual Studio Open Folder as a first-class Codex project root.");
        RequireContains(workspace, "FindRepositoryRoot(startDirectory)", "Workspace discovery must promote src-hosted solutions to the repository root.");
        RequireContains(workspace, "Directory.Exists(gitPath) || File.Exists(gitPath)", "Repository discovery must handle normal Git folders and Git worktree files.");
        RequireContains(workspace, "GetActiveProjectDirectory", "Single-project Visual Studio sessions must still get a stable workspace root.");
        RequireContains(workspace, "GetSolutionPath(dte)", "Workspace refresh must ask Visual Studio for the current solution path each time.");
        RequireContains(workspace, "SVsSolution", "Workspace refresh must fall back to SVsSolution when DTE has not populated Solution.FullName yet.");
        RequireContains(workspace, "GetActiveDocumentDirectory", "Workspace discovery must fall back to the active document directory when solution and project data are still loading.");
        RequireContains(workspace, "BuildWorkspaceIdentity", "Workspace refresh must create a stable project identity for Codex and memory systems.");
        RequireContains(workspace, "ComputeWorkspaceIdentityId", "Workspace identity must be deterministic across Visual Studio sessions.");
        RequireContains(workspace, "ComputeWorkspaceIdentityId(repositoryRemote, workspaceRoot)", "Workspace identity must be anchored to the opened repository or folder, not the specific solution file.");
        RequireContains(workspace, "ReadRepositoryRemote", "Workspace identity should include the repository remote when available.");
        RequireDoesNotContain(workspace, "vscodex-workspace.json", "VSCodex must not create redundant repository .codex workspace metadata.");
        RequireDoesNotContain(localPaths, "MemoryFile", "VSCodex must not expose a repository-local JSON memory fallback path.");
        RequireContains(models, "public sealed class WorkspaceIdentity", "The run model must carry explicit workspace identity data.");
        RequireContains(models, "[JsonObject(MemberSerialization.OptOut)]", "Persisted ReactiveObject models must opt out of the base serialization shape.");
        RequireContains(models, "public sealed class ExtensionSettings", "Workspace settings must remain a concrete serializable model.");
        RequireContains(models, "public sealed class AgentRoleDefinition", "Persisted agent definitions must remain concrete serializable models.");
        RequireContains(models, "public sealed class ChatMessage", "Saved sessions must remain concrete serializable models.");
        RequireContains(models, "SolutionRelativePath", "Workspace identity must distinguish solutions inside the same repository.");
        RequireContains(models, "RepositoryRemote", "Workspace identity must retain the Git remote for cross-product context matching.");
        RequireContains(models, "MemoryRoot", "Workspace identity must expose the project memory root.");
        RequireContains(viewModel, "_workspace.Refresh();", "Run must refresh Visual Studio workspace context immediately before executing Codex.");
        RequireContains(viewModel, "EnsureWorkspaceReadyForRun", "Run must block before Codex if Visual Studio has not supplied a real workspace root.");
        RequireContains(viewModel, "LocalPaths.ExtensionInstallRoot", "Run must reject the installed VSIX payload folder as an execution workspace.");
        RequireContains(viewModel, "_lastWorkspaceIdentityId", "Changing Visual Studio solutions must reset VSCodex execution state.");
        RequireContains(viewModel, "ThreadId = null", "Changing Visual Studio solutions or starting a new conversation must not reuse another workspace's Codex thread.");
        RequireContains(viewModel, "_codex.Cancel()", "Changing Visual Studio workspaces must cancel any stale in-flight Codex process.");
        RequireContains(viewModel, "WorkspaceIdentity = _workspace.CurrentWorkspaceIdentity", "Run and analytics requests must use the same workspace identity resolved by Visual Studio.");
        RequireContains(promptBuilder, "Workspace root:", "The enriched prompt must clearly tell Codex which repository root is the execution root.");
        RequireContains(promptBuilder, "Workspace identity:", "The enriched prompt must include the stable project identity.");
        RequireContains(promptBuilder, "Project memory root:", "The enriched prompt must include the project memory root.");
        RequireContains(promptBuilder, "Recovered ReactiveMemory context", "The enriched prompt must include recovered ReactiveMemory context before the user request.");
        RequireContains(promptBuilder, "Scope all memory operations to workspace identity", "ReactiveMemory hooks must be project-scoped to avoid cross-repository memory bleed.");
        RequireContains(sdkClient, "[\"workspaceRoot\"] = request.WorkspaceRoot", "The SDK payload must run from the repository root resolved by Visual Studio.");
        RequireContains(sdkClient, "[\"workspaceIdentity\"]", "The SDK payload must include workspace identity metadata for bridge-aware Codex SDKs.");
        RequireContains(sdkClient, "Visual Studio has not provided a solution or project workspace root yet", "SDK transport must reject missing workspace roots before starting Node.");
        RequireContains(cliClient, "Visual Studio has not provided a solution or project workspace root yet", "CLI fallback must reject missing workspace roots before starting Codex.");
        RequireContains(bridge, "VSCodex workspaceRoot is required", "Node bridge must reject missing workspace roots before SDK or CLI fallback execution.");
        RequireContains(bridge, "threads.set(request.threadId, { thread, workspaceRoot: request.workspaceRoot })", "SDK bridge must scope cached threads by Visual Studio workspace root.");
        RequireContains(bridge, "cwd: request.workspaceRoot", "Resilient CLI parsing must execute from the Visual Studio workspace root.");
        RequireDoesNotContain(bridge, "process.cwd()", "The bridge must never fall back to the installed VSIX payload directory.");
        RequireContains(orchestrator, "WorkspaceIdentity = request.WorkspaceIdentity", "Failover and enriched requests must preserve workspace identity.");
        RequireContains(orchestrator, "ReactiveMemoryContext = request.ReactiveMemoryContext", "Failover and enriched requests must preserve recovered ReactiveMemory context.");
        RequireContains(orchestration, "WorkspaceIdentity = request.WorkspaceIdentity", "Multi-agent section and synthesis requests must preserve workspace identity.");
        RequireContains(orchestration, "ReactiveMemoryContext = request.ReactiveMemoryContext", "Multi-agent section and synthesis requests must preserve recovered ReactiveMemory context.");
        RequireContains(memoryStore, "_workspaceMemories", "The in-window memory cache must be workspace-scoped without creating repository files.");
        RequireDoesNotContain(memoryStore, "memory.json", "Workspace memory must be durable through ReactiveMemory instead of a repository-local JSON file.");
        RequireContains(reactiveMemory, "ScanWorkspaceAsync", "ReactiveMemory must expose a ProjectMiner scan entry point for the active Visual Studio workspace.");
        RequireContains(reactiveMemory, "BuildProjectMinerFallbackInvocations", "VSCodex must provide a ProjectMiner-compatible fallback when the server does not expose a miner tool.");
        RequireContains(reactiveMemory, "\"project_miner\"", "ProjectMiner fallback drawers must be attributable to the repository scan.");
        RequireContains(reactiveMemory, "MaxAutomaticProjectMinerChunks", "Automatic ProjectMiner startup scans must be bounded so Visual Studio load is not dominated by memory writes.");
        RequireContains(reactiveMemory, "HasRecentAutomaticScan", "Automatic ProjectMiner scans must be persisted and throttled across Visual Studio sessions.");
        RequireContains(solutionMonitor, "IVsSolutionEvents", "The package must subscribe to Visual Studio solution events.");
        RequireContains(solutionMonitor, "OnAfterOpenSolution", "ReactiveMemory ProjectMiner must run when a solution opens.");
        RequireContains(solutionMonitor, "ScanWorkspaceAsync", "Solution-load monitoring must trigger ReactiveMemory ProjectMiner scanning.");
        RequireContains(solutionMonitor, "if (result.Success)", "A failed startup scan must not block the solution-open ProjectMiner retry.");
        RequireContains(solutionMonitor, "_lastQueuedWorkspaceId = identity.Id;", "Successful ProjectMiner scans must mark the workspace as completed.");
        RequireContains(solutionMonitor, "AutomaticScanDelay", "ProjectMiner scanning must be delayed until after Visual Studio startup settles.");
        RequireContains(solutionMonitor, "_scanInProgress", "ProjectMiner scanning must not run multiple concurrent repository scans.");
        RequireContains(solutionMonitor, "_scanRetryCount < 1", "ProjectMiner startup retries must be bounded to avoid repeated load spikes.");
        RequireContains(package, "InitializeReactiveMemoryProjectMinerAsync", "The package must initialize solution-load ProjectMiner scanning during background load.");
        RequireContains(viewModel, "ScanProjectMemoryCommand", "The Memory tab must expose an explicit full ProjectMiner scan action.");
    }

    [Test]
    public void Tool_window_checks_Codex_SDK_prerequisites_on_startup_and_shows_Windows_setup()
    {
        var bridge = ReadText("src/VSCodex/Resources/codex-bridge.mjs");
        var environmentService = ReadText("src/VSCodex/Services/CodexEnvironmentService.cs");
        var sdkClient = ReadText("src/VSCodex/Services/CodexSdkJsonClient.cs");
        var localPaths = ReadText("src/VSCodex/Infrastructure/LocalPaths.cs");
        var appBuilder = ReadText("src/VSCodex/Infrastructure/RxAppBuilder.cs");
        var viewModel = ReadText("src/VSCodex/ViewModels/VSCodexToolWindowViewModel.cs");
        var view = ReadText("src/VSCodex/Views/VSCodexToolWindowControl.xaml");
        var models = ReadText("src/VSCodex/Models/CodexModels.cs");

        RequireContains(bridge, "process.argv.includes('--check')", "The Node bridge must expose a startup health check path.");
        RequireContains(bridge, "process.argv.includes('--self-test-resilient-parser')", "The Node bridge must expose a non-network parser self-test for Windows codex stdout noise.");
        RequireContains(bridge, "request.command === 'getRateLimits'", "The Node bridge must expose a telemetry-only rate-limit command for tool-window load.");
        RequireContains(bridge, "app-server', '--listen', 'stdio://'", "The bridge must use Codex app-server stdio to read real account rate limits.");
        RequireContains(bridge, "account/rateLimits/read", "The bridge must call the Codex account rate-limit JSON-RPC method.");
        RequireContains(bridge, "method: 'account/rateLimits/read' }", "The bridge must call account/rateLimits/read without a params:null member, matching the generated Codex app-server protocol.");
        RequireDoesNotContain(bridge, "method: 'account/rateLimits/read', params: null", "The bridge must not send params:null because Codex app-server ignores that malformed request shape.");
        RequireContains(bridge, "account/rateLimits/updated", "The bridge must handle live Codex rate-limit update notifications.");
        RequireContains(bridge, "cmd.exe', ['/d', '/s', '/c', 'npm root -g']", "The bridge must query global npm packages through cmd.exe on Windows.");
        RequireContains(bridge, "path.join(packageRoot, exported)", "The bridge must resolve the package export file instead of importing a global package directory.");
        RequireContains(bridge, "npm install -g @openai/codex-sdk", "Bridge health check failures must include the SDK install command.");
        RequireContains(bridge, "buildThreadOptions", "The SDK bridge must pass workspace and execution options when creating or resuming a thread.");
        RequireContains(bridge, "workingDirectory = request.workspaceRoot", "The SDK bridge must run Codex from the Visual Studio workspace, not the VSIX install folder.");
        RequireContains(bridge, "skipGitRepoCheck = true", "The SDK bridge must allow non-git Visual Studio solution folders in non-interactive mode.");
        RequireContains(bridge, "modelReasoningEffort", "The SDK bridge must pass reasoning effort through the SDK's thread option name.");
        RequireContains(bridge, "normalizeApprovalPolicy", "The SDK bridge must normalize C# enum values to Codex SDK approval policy values.");
        RequireContains(bridge, "normalizeSandboxMode", "The SDK bridge must normalize C# enum values to Codex SDK sandbox mode values.");
        RequireContains(bridge, "resumeThread(request.threadId, options)", "Resumed SDK threads must receive the same workspace and execution options as new threads.");
        RequireContains(bridge, "runResilientCodexExec", "The bridge must retry through a resilient codex exec parser when the SDK cannot parse Windows stdout noise.");
        RequireContains(bridge, "runSdkThread", "The bridge must use SDK streaming so Codex rate-limit telemetry events are not discarded by the completed-turn API.");
        RequireContains(bridge, "thread.runStreamed", "The SDK bridge must consume streamed Codex events when available.");
        RequireContains(bridge, "emitCodexProgress(event)", "The SDK bridge must forward streamed progress while long-running requests are active.");
        RequireContains(bridge, "type: 'progress'", "The SDK bridge must emit user-visible progress events.");
        RequireContains(bridge, "type: 'rate-limits'", "The SDK bridge must emit rate-limit events to the WPF view model.");
        RequireContains(bridge, "VSCodex is finalizing the response; you can queue the next prompt", "Codex turn-completed progress must make the prompt queue availability explicit without claiming the visible task is complete.");
        RequireContains(bridge, "you can queue the next prompt", "Completed command-execution items must tell users they can continue prompting instead of implying the workflow is locked.");
        RequireDoesNotContain(bridge, "waiting for the final response", "Progress wording must not imply that the VSCodex prompt is blocked until a final response arrives.");
        RequireDoesNotContain(bridge, "Codex turn completed", "Interim progress must not say the Codex turn is complete before VSCodex has delivered the assistant response.");
        RequireContains(bridge, "codex.rate_limits", "The SDK bridge parser must preserve real Codex rate-limit telemetry events.");
        RequireContains(bridge, "rateLimits: state.rateLimits", "The SDK bridge result must expose real Codex rate-limit telemetry to the WPF view model.");
        RequireContains(bridge, "isSdkJsonNoiseError", "The bridge retry must be limited to the known SDK JSON parsing failure.");
        RequireContains(bridge, "Failed to parse item: SUCCESS: The process with PID", "The bridge must recognize the Codex SDK failure caused by Windows taskkill stdout.");
        RequireContains(bridge, "resolveCodexExecutable", "The bridge must resolve the native Codex executable for resilient fallback parsing.");
        RequireContains(bridge, "codex-sdk', 'node_modules', '@openai', 'codex-win32-x64'", "The resilient bridge runner must use the native Codex executable bundled with @openai/codex-sdk when the optional CLI is not installed.");
        RequireContains(bridge, "--experimental-json", "The bridge resilient runner must use Codex JSON event output.");
        RequireContains(bridge, "isProcessTerminationNoise", "The bridge resilient runner must filter Windows taskkill success lines.");
        RequireContains(bridge, "SUCCESS: The process with PID", "The bridge resilient runner must recognize the exact Windows taskkill success text.");
        RequireContains(bridge, "line.trim().startsWith('{')", "The bridge resilient runner must ignore non-JSON stdout before parsing Codex events.");
        RequireContains(bridge, "processCodexOutputLine", "The bridge resilient runner must use a testable parser for Codex JSON event lines.");
        RequireContains(bridge, "finalizeCodexEventState", "The bridge resilient runner must return the same result shape from test and runtime parsing.");
        RequireContains(bridge, "stdin.write(request.prompt", "The bridge resilient runner must still pass the enriched prompt through stdin.");
        RequireContains(bridge, "approval_policy=", "The bridge resilient runner must pass approval policy through current codex exec config overrides.");
        RequireContains(bridge, "model_reasoning_effort=", "The bridge resilient runner must pass reasoning effort through current codex exec config overrides.");
        RequireDoesNotContain(bridge, "options.cwd", "The SDK bridge must not use unsupported per-turn cwd options.");
        RequireContains(environmentService, "ICodexEnvironmentService", "VSCodex must have a dedicated Codex environment/prerequisite service.");
        RequireContains(environmentService, "CheckCodexSdkAsync", "Startup checks must verify that @openai/codex-sdk is importable.");
        RequireContains(localPaths, "ExtensionInstallRoot", "Bundled VSCodex resources must be resolved from the installed extension assembly location.");
        RequireContains(localPaths, "BundledBridgeScript", "The bundled bridge script path must be centralized.");
        RequireContains(localPaths, "typeof(LocalPaths).Assembly.Location", "The extension install root must be based on the VSCodex assembly path, not the Visual Studio process path.");
        RequireContains(environmentService, "LocalPaths.BundledBridgeScript", "The startup SDK check must find codex-bridge.mjs in the installed VSIX folder.");
        RequireContains(environmentService, "LocalPaths.ExtensionInstallRoot", "The SDK bridge health check must run from the installed extension folder.");
        RequireContains(sdkClient, "LocalPaths.BundledBridgeScript", "The runtime SDK bridge must launch the same VSIX-bundled bridge script that setup checks.");
        RequireContains(sdkClient, "LocalPaths.ExtensionInstallRoot", "The runtime SDK bridge must use the installed extension folder as its working directory.");
        RequireDoesNotContain(environmentService, "Path.Combine(AppDomain.CurrentDomain.BaseDirectory, \"Resources\", \"codex-bridge.mjs\")", "The setup check must not look for bridge resources under devenv.exe.");
        RequireDoesNotContain(sdkClient, "Path.Combine(AppDomain.CurrentDomain.BaseDirectory, \"Resources\", \"codex-bridge.mjs\")", "The runtime bridge must not look for bridge resources under devenv.exe.");
        RequireContains(environmentService, "winget install OpenJS.NodeJS.LTS", "Windows setup must explain how to install Node.js.");
        RequireContains(environmentService, "npm install -g @openai/codex-sdk", "Windows setup must explain how to install the Codex SDK.");
        RequireContains(environmentService, "npm install -g @openai/codex", "Windows setup must explain the optional CLI fallback install.");
        RequireContains(environmentService, "CreateProcessStartInfo", "Environment checks and runtime clients must share process launch behavior.");
        RequireContains(environmentService, "/d /s /c call", "Windows .cmd launchers must run through cmd.exe when UseShellExecute is false.");
        RequireContains(appBuilder, "RegisterSingleton<ICodexEnvironmentService>", "The prerequisite service must be registered with the ReactiveUI app builder.");
        RequireContains(viewModel, "CheckPrerequisitesCommand", "The tool-window view model must expose an explicit setup check command.");
        RequireContains(viewModel, "CopyPrerequisiteCommand", "Missing setup requirements must expose command-copy actions.");
        RequireContains(viewModel, "UpdatePrerequisiteCommand", "Missing or outdated setup requirements must expose update actions.");
        RequireContains(view, "Header=\"Setup\"", "Prerequisite actions must be available from the controls pane.");
        RequireContains(view, "CanCopyCommand", "Prerequisite command copy buttons must only appear for unmet requirements.");
        RequireContains(view, "CanUpdate", "Prerequisite update buttons must only appear when an action command exists.");
        RequireContains(viewModel, "EnsureCodexSdkReadyForRunAsync", "Run must be blocked with guidance when the SDK is missing.");
        RequireContains(viewModel, "CheckPrerequisitesAsync", "Startup must check VSCodex prerequisites.");
        RequireContains(viewModel, "AddMessage(CodexMessageRole.System, CodexSetupInstructions)", "Missing prerequisites must still write Windows setup instructions into the conversation.");
        RequireContains(viewModel, "Open Tools > Options > VSCodex", "Setup failures must point users to the Tools > Options settings surface instead of the removed in-window settings tab.");
        RequireContains(models, "PrerequisiteStatus", "Prerequisite status must be modeled for the setup UI.");
        RequireContains(models, "CodexEnvironmentReport", "The environment check report must be modeled.");
    }

    [Test]
    public async Task Local_machine_Codex_SDK_bridge_check_passes()
    {
        var result = await RunProcessAsync("node", Quote(PathFor("src/VSCodex/Resources/codex-bridge.mjs")) + " --check");
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Local VSCodex SDK bridge check failed. Install on Windows with `npm install -g @openai/codex-sdk` and restart Visual Studio." + Environment.NewLine + result.Output + Environment.NewLine + result.Error);
        }

        RequireContains(result.Output, "Codex SDK bridge prerequisites OK", "The local SDK bridge check must confirm the installed Codex SDK.");
    }

    [Test]
    public async Task Local_bridge_resilient_parser_filters_windows_taskkill_stdout_noise()
    {
        var result = await RunProcessAsync("node", Quote(PathFor("src/VSCodex/Resources/codex-bridge.mjs")) + " --self-test-resilient-parser");
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Local VSCodex resilient parser self-test failed." + Environment.NewLine + result.Output + Environment.NewLine + result.Error);
        }

        RequireContains(result.Output, "\"threadId\":\"thread-test\"", "The resilient parser must preserve the Codex thread id.");
        RequireContains(result.Output, "\"finalResponse\":\"Hi from parser\"", "The resilient parser must preserve the assistant response after filtering noise.");
        RequireContains(result.Output, "\"primary\":{\"used_percent\":10", "The resilient parser must preserve the real five-hour Codex rate-limit event.");
        RequireContains(result.Output, "\"secondary\":{\"used_percent\":34", "The resilient parser must preserve the real weekly Codex rate-limit event.");
        RequireContains(result.Output, "\"ignoredCount\":1", "The resilient parser must ignore the Windows taskkill success line instead of treating it as JSON.");
    }

    [Test]
    public void Tool_window_streaming_events_and_collections_are_marshaled_to_the_WPF_dispatcher()
    {
        var viewModel = ReadText("src/VSCodex/ViewModels/VSCodexToolWindowViewModel.cs");

        RequireContains(viewModel, "Dispatcher _uiDispatcher", "The tool-window view model must capture the WPF dispatcher that owns bound collections.");
        RequireContains(viewModel, "System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher", "The captured dispatcher must be the active WPF application dispatcher when available.");
        RequireContains(viewModel, "RunOnUiThread", "Codex streaming events must marshal UI-bound mutations through a common dispatcher helper.");
        RequireContains(viewModel, "_uiDispatcher.CheckAccess()", "The dispatcher helper must avoid re-dispatching when already on the UI thread.");
        RequireContains(viewModel, "DispatcherScheduler(_uiDispatcher)", "ReactiveCommand can-execute notifications must be raised on the WPF dispatcher.");
        RequireContains(viewModel, "ObserveOn(_uiScheduler)", "ReactiveCommand can-execute observables must observe on the WPF dispatcher.");
        RequireContains(viewModel, "ObserveOnSafe(_uiScheduler)", "Streaming subscriptions must observe on the same WPF dispatcher scheduler.");
        RequireContains(viewModel, "CreateFromTask<McpServerDefinition>(SelectMcpServerAsync, null, _uiScheduler)", "The MCP tools command must use the WPF dispatcher scheduler.");
        RequireContains(viewModel, "await _joinableTaskFactory.SwitchToMainThreadAsync();", "MCP tool discovery must return to the Visual Studio UI thread before bound UI updates.");
        RequireContains(viewModel, "_joinableTaskFactory.SwitchToMainThreadAsync()", "Background event callbacks must post collection updates back through the Visual Studio UI thread.");
        RequireContains(viewModel, "AddMessage(CodexMessageRole.Assistant, ev.Message)", "Codex stdout/message events must still append assistant messages.");
        RequireContains(viewModel, "Messages.Add(message)", "Messages must continue to be added to the bound collection after marshaling.");
        RequireContains(viewModel, "var snapshot = items.ToList();", "Collection replacements must snapshot source data before dispatching to the UI thread.");
    }

    [Test]
    public void Tool_window_layout_uses_visual_studio_theme_resources_for_common_controls()
    {
        var view = ReadText("src/VSCodex/Views/VSCodexToolWindowControl.xaml");
        var viewModel = ReadText("src/VSCodex/ViewModels/VSCodexToolWindowViewModel.cs");
        var codeBehind = ReadText("src/VSCodex/Views/VSCodexToolWindowControl.xaml.cs");
        var voiceInput = ReadText("src/VSCodex/Services/VoiceInputService.cs");
        var models = ReadText("src/VSCodex/Models/CodexModels.cs");
        var reactiveMemory = ReadText("src/VSCodex/Services/ReactiveMemoryService.cs");
        var memoryStore = ReadText("src/VSCodex/Services/MemoryStore.cs");

        RequireContains(view, "MinWidth=\"240\"", "The tool window must allow narrow docking while the inner controls wrap and scroll.");
        RequireContains(view, "MaxWidth=\"{Binding ActualWidth, ElementName=Root}\"", "The settings panel must be constrained to the actual tool-window width.");
        RequireContains(view, "Width=\"620\"", "The controls panel must provide enough width for MCP, memory, analytics, and agent settings.");
        RequireContains(view, "Grid.RowSpan=\"2\"", "The controls panel must span the conversation and prompt rows so dense MCP controls are usable.");
        RequireContains(view, "PromptResizeThumbStyle", "The prompt input must expose a theme-aware mouse resize grip.");
        RequireContains(view, "DragStarted=\"OnPromptResizeDragStarted\"", "Prompt resizing must have a guarded drag lifecycle before height changes.");
        RequireContains(view, "DragDelta=\"OnPromptResizeDragDelta\"", "Dragging the prompt resize grip must resize the prompt input.");
        RequireContains(view, "DragCompleted=\"OnPromptResizeDragCompleted\"", "Prompt resizing must save the final height only after the mouse drag completes.");
        RequireContains(view, "PreviewMouseLeftButtonUp=\"OnPromptResizeMouseLeftButtonUp\"", "Prompt resizing must release mouse capture when the button is released.");
        RequireContains(view, "LostMouseCapture=\"OnPromptResizeLostMouseCapture\"", "Prompt resizing must recover if Visual Studio or WPF interrupts mouse capture.");
        RequireContains(view, "MinHeight=\"32\"", "The prompt input must keep a one-line minimum height when collapsed.");
        RequireContains(view, "MaxHeight=\"600\"", "The prompt input must have a practical maximum height when expanded.");
        RequireContains(view, "Panel.ZIndex=\"10\"", "The settings panel must overlay the conversation in narrow layouts instead of disappearing off-screen.");
        RequireContains(view, "Grid.Row=\"3\"", "The prompt footer must wrap model controls and run buttons below the resizable prompt input.");
        RequireContains(view, "Header=\"History\"", "Conversation history must replace the obsolete in-window settings tab.");
        RequireContains(view, "VisibleHistoryItems", "The history tab must show saved VSCodex conversation sessions.");
        RequireContains(view, "Rate limits remaining", "The tool window must surface hourly and weekly rate-limit details near the model controls.");
        RequireContains(view, "x:Name=\"ConversationActionPanel\"", "Conversation actions and prompt actions must be grouped in one button panel above the prompt input.");
        RequireContains(view, "Content=\"Review selection\"", "The unified action panel must include the selected-code review action.");
        RequireContains(view, "Content=\"Fix active errors\"", "The unified action panel must include active error assistance.");
        RequireContains(view, "Content=\"Write tests\"", "The unified action panel must include test generation.");
        RequireContains(view, "Content=\"Plan work\"", "The unified action panel must include planning.");
        RequireContains(view, "Content=\"+ Reference\"", "The unified action panel must include reference refresh.");
        RequireContains(view, "Content=\"Docs\"", "The unified action panel must include documentation generation.");
        RequireContains(view, "ItemsSource=\"{Binding RunActivityRoots}\"", "Conversation feedback must render the collapsible activity tree instead of the flat message list.");
        RequireContains(view, "HierarchicalDataTemplate DataType=\"{x:Type models:RunActivityNode}\"", "Activity history must use a tree template so each user request owns its child actions.");
        RequireContains(view, "Command=\"{Binding DataContext.OpenActivityFileCommand, RelativeSource={RelativeSource AncestorType=TreeView}}\"", "Changed files in the activity tree must be openable from Visual Studio.");
        RequireContains(view, "Binding=\"{Binding IsDeleted}\"", "Deleted changed files must have a distinct visual state.");
        RequireContains(view, "Foreground\" Value=\"#E81123\"", "Deleted changed files must be shown in red text.");
        RequireContains(viewModel, "UseMessageAsPromptCommand", "The view model must support reusing a prior user prompt as editable input.");
        RequireContains(viewModel, "CopyMessageCommand", "The view model must still expose copy support for messages used by saved history and commands.");
        RequireContains(viewModel, "System.Windows.Clipboard.SetText", "The view model must copy message text to the Windows clipboard.");
        RequireContains(view, "Command=\"{Binding NewThreadCommand}\"", "The tool window must expose a first-class new thread action.");
        RequireContains(view, "AutomationProperties.Name=\"New VSCodex thread\"", "Icon-only header controls must have an accessible automation name.");
        RequireContains(view, "ToolTip=\"Start a fresh VSCodex thread.", "The new-thread action must be an icon button with an accessible tooltip.");
        RequireContains(view, "AutomationProperties.Name=\"Open VSCodex history\"", "The history icon must have an accessible automation name.");
        RequireContains(view, "ToolTip=\"Open VSCodex conversation history.", "The history action must be an icon button with an accessible tooltip.");
        RequireDoesNotContain(view, "Content=\"Controls\"", "The controls entry point must be a symbolic header button.");
        RequireContains(view, "AutomationProperties.Name=\"Open VSCodex controls\"", "The controls entry point must have an accessible automation name.");
        RequireContains(view, "Open VSCodex controls for setup", "The controls cog must remain discoverable through an accessible tooltip.");
        RequireContains(view, "Click=\"OnOpenToolPanelClick\"", "The controls entry point must open the tool panel without relying on slash commands.");
        RequireContains(codeBehind, "private void OnOpenToolPanelClick", "The controls entry point must be wired in the view code-behind.");
        RequireContains(codeBehind, "ViewModel.IsToolPanelOpen = true", "Clicking Controls must reveal the tool panel.");
        RequireContains(view, "<Setter Property=\"ToolTip\" Value=\"Send\" />", "The tool window run control must expose the visual send state through a tooltip.");
        RequireContains(view, "<Setter Property=\"ToolTip\" Value=\"Stop\" />", "The same visual run control must expose the stop state while a task is running.");
        RequireContains(view, "x:Name=\"SendIcon\"", "The idle run control must show a send icon.");
        RequireContains(view, "x:Name=\"StopIcon\"", "The running run control must show a stop icon.");
        RequireContains(view, "Click=\"OnRunControlClick\"", "The visual run control must route send and stop through one button.");
        RequireContains(view, "IsRunControlInStopMode", "The run control must only show the stop state when there is no prompt text to queue.");
        RequireContains(viewModel, "Queue<string> _queuedPrompts", "VSCodex must keep accepting prompts while a long Codex turn is active.");
        RequireContains(viewModel, "RunCommand = ReactiveCommand.CreateFromTask(SubmitPromptAsync", "Run command execution must submit or queue a prompt without waiting for the full Codex turn.");
        RequireContains(viewModel, "ProcessRunQueueAsync", "Queued prompts must be processed sequentially in the background.");
        RequireContains(viewModel, "Queued VSCodex request", "Users must get visible feedback when a prompt is queued behind an active run.");
        RequireContains(viewModel, "IsRunControlInStopMode => IsRunning && !HasPromptText", "Typing a prompt during an active run must switch the run button back to send mode.");
        RequireContains(codeBehind, "ViewModel.IsRunControlInStopMode ? ViewModel.CancelCommand : ViewModel.RunCommand", "The shared run button must queue typed prompts during active runs instead of always cancelling.");
        RequireContains(view, "IsIndeterminate=\"True\"", "The running stop state must show an animated progress element.");
        RequireDoesNotContain(view, "Content=\"Run\"", "The prompt footer must not use a text Run button.");
        RequireDoesNotContain(view, "Content=\"Cancel\"", "The prompt footer must not use a text Cancel button.");
        RequireDoesNotContain(view, "Content=\"New Thread\"", "The header must use a visual new-thread button instead of text.");
        RequireDoesNotContain(view, "Content=\"Settings\"", "The header must not expose the old settings button.");
        RequireContains(view, "BooleanToVisibilityConverter", "The controls tab strip must be hidden until the user opens it.");
        RequireContains(view, "IsToolPanelOpen", "The controls tab strip must not occupy workspace permanently.");
        RequireContains(view, "CanEditSettings", "Model and settings controls must be locked while a task is running.");
        RequireContains(view, "OnCloseToolPanelClick", "The controls panel must have an explicit close action.");
        RequireContains(view, "AutomationProperties.Name=\"Close VSCodex controls\"", "The controls panel close action must be accessible.");
        RequireContains(view, "PreviewKeyDown=\"OnPromptPreviewKeyDown\"", "The prompt box must support keyboard shortcuts.");
        RequireContains(view, "AutomationProperties.Name=\"VSCodex prompt input\"", "The prompt box must have an accessible name.");
        RequireContains(view, "Enter sends, Ctrl+Enter inserts a newline", "The prompt UI must show the Visual Studio shortcut behavior.");
        RequireContains(view, "PromptSuggestionPopup", "The prompt box must show inline VSCodex suggestions for /, @, and # tokens.");
        RequireContains(view, "ItemsSource=\"{Binding PromptSuggestions}\"", "Inline prompt suggestions must be backed by the view-model suggestion list.");
        RequireContains(view, "IsOpen=\"{Binding IsPromptSuggestionOpen, Mode=TwoWay}\"", "The prompt suggestion popup must be controlled by view-model state.");
        RequireContains(view, "OnPromptSuggestionDoubleClick", "Prompt suggestions must be insertable with the mouse.");
        RequireContains(view, "Header=\"Context\"", "Context-sensitive file and selection references must be grouped in the tool pane.");
        RequireContains(view, "Header=\"MCP\"", "MCP servers must remain a first-class tool panel surface.");
        RequireContains(view, "SelectedMcpServer.Name", "The MCP tab must edit the selected server in a dedicated detail pane.");
        RequireContains(view, "GridSplitter", "Dense tool panels must provide resizable regions instead of fixed tiny rows.");
        RequireContains(view, "Header=\"Tool input\"", "MCP tool argument editing must have a dedicated input pane.");
        RequireContains(view, "Click=\"OnToggleVoiceInputClick\"", "The prompt footer voice button must route clicks directly to voice capture.");
        RequireContains(codeBehind, "private void OnToggleVoiceInputClick", "The voice input button must have an explicit click handler.");
        RequireContains(codeBehind, "ViewModel.ToggleVoiceInput();", "Clicking the voice input button must invoke the view-model toggle without WPF command parameter conversion.");
        RequireContains(view, "VoiceInputButtonStyle", "The voice input button must have a distinct active-listening visual state.");
        RequireContains(view, "VoiceListeningDot", "The voice input button must show a visible recording dot while listening.");
        RequireContains(view, "Text=\"Listening\"", "The prompt footer must make the listening state obvious without relying on tooltip hover.");
        RequireContains(view, "VoiceInputStatus", "Voice input must surface ready, listening, and unavailable states.");
        RequireContains(viewModel, "VoiceTranscriptRevision", "Voice transcript appends must notify the view so dictated text is visibly synchronized into the prompt input.");
        RequireContains(codeBehind, "SyncPromptTextBoxAfterVoiceTranscript", "The prompt input must refresh after voice transcripts even when the text box has focus.");
        RequireContains(codeBehind, "GetBindingExpression(TextBox.TextProperty)?.UpdateTarget()", "Voice transcript synchronization must preserve the prompt binding and pull the latest view-model prompt.");
        RequireDoesNotContain(codeBehind, "PromptTextBox.Text =", "Prompt code-behind must not replace the bound Text property and break later voice transcript updates.");
        RequireContains(viewModel, "TryExtractVoiceSubmit", "Voice transcripts must understand spoken send commands.");
        RequireContains(viewModel, "VoiceSubmitOnlyCommands", "Voice input must support short spoken submit commands like send or run.");
        RequireContains(voiceInput, "Task.Run(StartListeningCore)", "Speech recognizer COM initialization must be lazy and off the Visual Studio startup UI path.");
        RequireContains(voiceInput, "InvalidComObjectException", "Voice input must catch separated RCW failures from System.Speech.");
        RequireContains(voiceInput, "BehaviorSubject<string>", "Voice input status must replay the current recognizer state to the tool-window view model after construction.");
        RequireContains(voiceInput, "InstalledRecognizers()", "Voice input must select an installed Windows speech recognizer instead of assuming the Visual Studio UI culture is installed.");
        RequireContains(voiceInput, "SpeechDetected", "Voice input must surface microphone activity while listening.");
        RequireContains(voiceInput, "SpeechRecognitionRejected", "Voice input must surface recognizer rejection feedback when speech is heard but not captured as text.");
        RequireDoesNotContain(voiceInput, "result.Confidence < 0.12", "Recognized dictation text must be inserted into the prompt instead of silently discarded by a confidence cutoff.");
        RequireContains(voiceInput, "low-confidence transcript; review it", "Low-confidence dictation must still be visible in the prompt with review guidance.");
        RequireContains(view, "FileTabSelectedBackgroundBrushKey", "Selected tool tabs must use Visual Studio file-tab selected background resources for contrast.");
        RequireContains(view, "FileTabSelectedTextBrushKey", "Selected tool tabs must use Visual Studio file-tab selected text resources for contrast.");
        RequireContains(view, "ContentSource=\"Header\"", "Tab headers must be rendered by the themed template so selected text remains readable.");
        RequireContains(view, "TargetType=\"ComboBox\"", "Combo boxes must receive explicit theme styling.");
        RequireContains(view, "AutomationProperties.Name=\"VSCodex request mode\"", "The request mode combo box must have an accessible name.");
        RequireContains(view, "AutomationProperties.Name=\"VSCodex model\"", "The model combo box must have an accessible name.");
        RequireContains(view, "ControlTemplate TargetType=\"{x:Type ComboBox}\"", "Combo boxes must use a Visual Studio themed template instead of the light WPF default template.");
        RequireContains(view, "PART_EditableTextBox", "Editable combo boxes must explicitly theme their internal text box.");
        RequireContains(view, "VSCodexComboBoxEditableTextBoxStyle", "Editable combo box text must have a dedicated Visual Studio themed style.");
        RequireContains(view, "EnvironmentColors.ComboBoxTextBrushKey", "Combo boxes must use Visual Studio foreground resources for contrast.");
        RequireContains(view, "EnvironmentColors.ComboBoxBackgroundBrushKey", "Combo boxes must use Visual Studio background resources for contrast.");
        RequireContains(view, "EnvironmentColors.ComboBoxDisabledTextBrushKey", "Disabled combo boxes must use Visual Studio disabled foreground resources.");
        RequireContains(view, "IsHitTestVisible=\"{Binding CanEditSettings}\"", "Running tasks must lock settings without applying WPF disabled colors to the whole settings surface.");
        RequireDoesNotContain(view, "IsEnabled=\"{Binding CanEditSettings}\"", "Settings locks must not use disabled WPF templates that create unreadable combo-box text.");
        RequireDoesNotContain(view, "SystemColors.WindowTextBrushKey", "Tool-window controls must not use system light-theme text resources in Visual Studio dark themes.");
        RequireDoesNotContain(view, "SystemColors.WindowBrushKey", "Tool-window controls must not use system light-theme background resources in Visual Studio dark themes.");
        RequireContains(view, "TargetType=\"Button\"", "Buttons must receive explicit theme styling.");
        RequireContains(view, "TargetType=\"CheckBox\"", "Check boxes must receive explicit theme styling.");
        RequireContains(view, "TargetType=\"ListBoxItem\"", "List items must receive explicit theme styling.");
        RequireContains(view, "TargetType=\"TreeViewItem\"", "Activity tree items must receive explicit theme styling.");
        RequireContains(view, "TargetType=\"GroupBox\"", "Grouped panes must receive explicit theme styling.");
        RequireContains(view, "EnvironmentColors.ToolWindowBackgroundBrushKey", "Tool-window backgrounds must use Visual Studio theme brushes.");
        RequireContains(view, "EnvironmentColors.ToolWindowTextBrushKey", "Tool-window foregrounds must use Visual Studio theme brushes.");
        RequireContains(view, "EnvironmentColors.ToolWindowBorderBrushKey", "Tool-window borders must use Visual Studio theme brushes.");
        RequireContains(view, "SystemColors.HighlightTextBrushKey", "Selected list items must use system highlight text for contrast.");
        RequireContains(view, "controls:MarkdownTextBlock", "Chat messages must render Markdown instead of showing raw Markdown source.");
        RequireContains(view, "Markdown=\"{Binding Detail}\"", "The Markdown renderer must bind directly to activity detail content.");
        RequireContains(view, "TargetType=\"{x:Type controls:MarkdownTextBlock}\"", "Rendered Markdown must use Visual Studio themed text resources.");
        RequireContains(ReadText("src/VSCodex/Controls/MarkdownTextBlock.cs"), "Hyperlink", "Rendered Markdown must support clickable Markdown links.");
        RequireContains(view, "x:Name=\"ConversationScrollViewer\"", "The activity history must have a named scroll container for auto-scrolling.");
        RequireContains(view, "ScrollViewer.CanContentScroll=\"False\"", "The conversation tree must use pixel-based scrolling so tall activity nodes are fully readable and not clipped at the viewport boundary.");
        RequireContains(viewModel, "RateLimits", "Rate-limit rows must be backed by view-model state.");
        RequireContains(viewModel, "RunActivityRoots", "Run feedback must be backed by a scrollable activity tree.");
        RequireContains(viewModel, "BeginRunActivity", "Each user prompt must create a root activity node.");
        RequireContains(viewModel, "AddDefaultActivitySections", "Activity roots must include standard child sections for agent, MCP, skill, file, assistant, and system output.");
        RequireContains(viewModel, "AddChangedFilesActivity", "Completed runs must show changed files in the activity tree.");
        RequireContains(viewModel, "CollectChangedFilesForWorkspace", "Completed runs must collect git changed files for user visibility.");
        RequireContains(viewModel, "StartRunProgress", "Run must add immediate activity feedback before long Codex calls complete.");
        RequireContains(viewModel, "VSCodex is working", "Run progress feedback must be visible in the activity history.");
        RequireContains(viewModel, "Observable.Interval(TimeSpan.FromSeconds(15)", "Long-running requests must refresh visible progress periodically.");
        RequireContains(viewModel, "RefreshRateLimitsAsync", "The tool window must refresh real Codex rate-limit telemetry on startup and workspace refresh.");
        RequireContains(viewModel, "RefreshWorkspaceIdentityForStartup", "Tool-window creation must avoid rebuilding the workspace file index during Visual Studio startup.");
        RequireContains(viewModel, "ScheduleStartupChecksInBackground", "Prerequisite and telemetry checks must be deferred from initial tool-window construction.");
        RequireContains(viewModel, "Task.Delay(TimeSpan.FromSeconds(4)", "Startup setup checks must be delayed so Visual Studio can finish loading visibly.");
        RequireContains(viewModel, "Array.Empty<WorkspaceFileReference>()", "Empty prompt startup updates must not trigger full file suggestion indexing.");
        RequireDoesNotContain(viewModel, "this.WhenAnyValue(x => x.Prompt).ThrottleDistinct(TimeSpan.FromMilliseconds(180), _uiScheduler).Subscribe(OnPromptChanged),\r\n            _voiceInput", "The tool-window view model must not dispose the singleton voice service when the pane closes.");
        RequireContains(viewModel, "_codex.GetRateLimitsAsync", "Rate-limit rows must be fed by the Codex bridge, not synthetic usage estimates.");
        RequireContains(viewModel, "Fetching Codex telemetry", "Users must see visible progress while rate-limit telemetry is loading.");
        RequireContains(viewModel, "Codex telemetry unavailable", "Users must see explicit telemetry failure state instead of stale waiting text.");
        RequireContains(viewModel, "UpdateRateLimitsFromJson", "SDK rate-limit telemetry must update the visible rate-limit rows.");
        RequireContains(viewModel, "Status = \"Running VSCodex...\"", "Run must give immediate visible feedback when a task starts.");
        RequireContains(viewModel, "Label = \"5h\"", "Rate-limit UI must expose the Codex five-hour window rather than a one-hour label.");
        RequireContains(viewModel, "FindRateLimitToken", "Rate-limit parsing must accept explicit remaining/reset telemetry when the SDK emits it.");
        RequireContains(viewModel, "\"primary\"", "Rate-limit parsing must understand the Codex primary five-hour telemetry window.");
        RequireContains(viewModel, "\"secondary\"", "Rate-limit parsing must understand the Codex secondary weekly telemetry window.");
        RequireContains(viewModel, "used_percent", "Rate-limit parsing must consume Codex used-percent telemetry.");
        RequireContains(viewModel, "usedPercent", "Rate-limit parsing must consume the current Codex app-server camelCase telemetry shape.");
        RequireContains(viewModel, "rateLimitsByLimitId.codex", "Rate-limit parsing must support the current Codex app-server multi-bucket payload.");
        RequireContains(viewModel, "100 - usedPercent.Value", "Codex used-percent telemetry must be converted to the remaining percentage shown by Codex.");
        RequireContains(viewModel, "FormatRateLimitReset", "Rate-limit reset times must be formatted to match the Codex UI.");
        RequireDoesNotContain(viewModel, "FindUsageToken", "Rate-limit rows must not fall back to synthetic usage-token estimates.");
        RequireDoesNotContain(viewModel, "UpdateObservedUsageFromToken", "Rate-limit rows must not show observed token estimates as real Codex limits.");
        RequireContains(viewModel, "result.usage", "SDK bridge result usage must be parsed from the current response shape.");
        RequireDoesNotContain(viewModel, "used \" + FormatTokenCount", "Observed token usage must not be surfaced as the real Codex rate-limit value.");
        RequireContains(viewModel, "CanEditSettings => !IsRunning", "Settings must not be editable while a task is running.");
        RequireContains(viewModel, "CanChangeSetting", "The view model must reject setting changes even if a delayed binding fires while a task is running.");
        RequireContains(viewModel, "QueueStatusDisplay", "Queued prompts must have visible composer feedback while Codex is running.");
        RequireContains(viewModel, "private const int ModelSettingsSaveDebounceMilliseconds = 350", "Model selector changes must be debounced before settings persistence.");
        RequireContains(viewModel, "ScheduleModelSettingsSave", "Model selector changes must not synchronously save settings on the Visual Studio UI thread.");
        RequireContains(viewModel, "Task.Delay(TimeSpan.FromMilliseconds(ModelSettingsSaveDebounceMilliseconds)", "Model setting persistence must wait until combo-box text and selection events settle.");
        RequireContains(viewModel, "SaveSettingsForWorkspace(workspaceIdentity, settings)", "Debounced model setting persistence must write from the captured workspace snapshot.");
        RequireContains(viewModel, "if (changed)", "Settings broadcasts must only refresh analytics when live tool-window settings actually changed.");
        RequireContains(viewModel, "collection.Zip(snapshot, EqualityComparer<T>.Default.Equals).All", "Settings broadcasts must not clear and rebuild combo-box item collections when values are unchanged.");
        RequireContains(view, "Text=\"{Binding SelectedModel, UpdateSourceTrigger=PropertyChanged, Delay=250}\"", "The editable model combo box must debounce text updates so changing models cannot re-enter WPF selection handling on every keystroke.");
        RequireDoesNotContain(viewModel, "SelectedModel { get => _selectedModel; set { if (!CanChangeSetting(_selectedModel, value)) return; this.RaiseAndSetIfChanged(ref _selectedModel, value); SaveModelSettings(); UpdateAnalytics(Prompt); } }", "Changing the current model must not synchronously save settings and recompute analytics from the property setter.");
        RequireContains(viewModel, "Math.Max(32d", "The persisted prompt height must accept the same one-line minimum as the mouse resize grip.");
        RequireContains(viewModel, "SetLiveInputAreaHeight", "Mouse resizing must preview height without saving settings on every drag tick.");
        RequireContains(viewModel, "CommitInputAreaHeight", "Mouse resizing must persist the prompt height once the drag completes.");
        RequireContains(viewModel, "SaveSettingsForCurrentWorkspace", "Settings changed from the tool window must be retained per Visual Studio solution.");
        RequireContains(viewModel, "ToggleVoiceInputCommand", "The view model must expose a voice-input toggle command.");
        RequireContains(viewModel, "public void ToggleVoiceInput()", "The view must be able to invoke voice input directly from the click handler.");
        RequireContains(view, "AutomationProperties.Name=\"Toggle VSCodex voice input\"", "The voice input button must be accessible.");
        RequireContains(view, "AutomationProperties.Name=\"Send or stop VSCodex request\"", "The visual run control must be accessible in send and stop states.");
        RequireContains(view, "AutomationProperties.Name=\"Stop VSCodex request\"", "A persistent stop control must remain available while typed prompts are queued during a running request.");
        RequireContains(view, "Visibility=\"{Binding IsPersistentStopControlVisible, Converter={StaticResource BooleanToVisibilityConverter}}\"", "The secondary stop control must be hidden when the primary run button is already in stop mode.");
        RequireContains(viewModel, "IsPersistentStopControlVisible => IsRunning && HasPromptText", "The persistent stop control must only appear when typed input makes the primary run control send another prompt.");
        RequireContains(view, "QueueStatusDisplay", "The composer must show queued prompt count beside the run control.");
        RequireContains(viewModel, "AppendVoiceTranscript", "Recognized speech must append to the current prompt.");
        RequireContains(viewModel, "Task.Run(async () => await _mcpTools.DiscoverToolsAsync", "MCP discovery must avoid blocking the Visual Studio UI thread.");
        RequireContains(viewModel, "Task.Run(() => _workspace.ResolveMentions", "Prompt reference resolution must avoid blocking the Visual Studio UI thread.");
        RequireContains(viewModel, "Task.Run(() => _memoryStore.Search", "Memory search during a run must avoid blocking the Visual Studio UI thread.");
        RequireContains(viewModel, "Task.Run(() => _modelAnalytics.Estimate", "Model analytics for a run must avoid blocking the Visual Studio UI thread.");
        RequireContains(viewModel, "Task.Run(() => CollectChangedFilesForWorkspace", "Changed-file collection must avoid blocking the Visual Studio UI thread.");
        RequireContains(memoryStore, "private readonly object _gate", "The in-window memory cache must serialize snapshot access used by background run preparation.");
        RequireContains(memoryStore, "_currentSnapshot", "The in-window memory cache must expose immutable snapshots instead of reading BehaviorSubject state across threads.");
        RequireContains(reactiveMemory, "catch (OperationCanceledException)", "ReactiveMemory host cancellations must be handled as unavailable context rather than crashing VSCodex.");
        RequireContains(reactiveMemory, "ReactiveMemory MCP call was cancelled by the host; continuing without blocking VSCodex.", "ReactiveMemory cancellation feedback must explain that VSCodex continues without blocking the run.");
        RequireContains(viewModel, "VSCodex settings are locked while a task is running", "Blocked setting changes must produce visible user feedback.");
        RequireContains(viewModel, "ShowHistoryCommand", "The header command must open the history tab on demand.");
        RequireContains(viewModel, "IsToolPanelOpen = true", "The tool panel must still open on demand.");
        RequireContains(viewModel, "RefreshHistory", "The history tab must refresh saved sessions.");
        RequireContains(viewModel, "LoadHistoryCommand", "The history tab must be able to reopen saved sessions.");
        RequireContains(viewModel, "DeleteHistoryCommand", "The history tab must be able to delete saved sessions.");
        RequireContains(viewModel, "BeginRenameHistoryCommand", "The history tab must support renaming saved sessions.");
        RequireContains(models, "WorkspaceIdentityId", "Conversation history must carry Visual Studio workspace identity metadata.");
        RequireContains(viewModel, "SessionBelongsToCurrentWorkspace", "History must default to the current Visual Studio solution or workspace.");
        RequireContains(viewModel, "ForkSessionForCurrentWorkspace", "Loading a known foreign-workspace transcript must not reuse another workspace's Codex thread.");
        RequireContains(view, "CurrentWorkspaceDisplay", "The tool window must surface the active Visual Studio workspace context.");
        RequireContains(viewModel, "persist: false", "Live progress messages must not be persisted as durable conversation history.");
        RequireContains(viewModel, "PromptSuggestions", "The view model must expose inline prompt suggestions.");
        RequireContains(viewModel, "UpdatePromptSuggestions", "Typing /, @, or # must update context-sensitive prompt suggestions.");
        RequireContains(viewModel, "CreateAgentPlanPrompt", "Plan work must populate the Agents plan preview instead of leaving the Plan pane unused.");
        RequireContains(viewModel, "RefreshAgentPlanPreview", "The Agents Plan pane must show a local plan preview before multi-agent execution events arrive.");
        RequireContains(viewModel, "AgentsToolTabIndex", "Plan work must open the Agents controls tab so users can see and edit the plan.");
        RequireContains(viewModel, "OrchestrationSections.Clear();", "Starting a new thread must clear stale agent plan sections.");
        RequireContains(viewModel, "TargetTab = \"browse-files\"", "Typing @ must offer a disk file picker for references outside the repository.");
        RequireContains(viewModel, "InsertFileReferencePaths", "Files selected from disk must be inserted into the prompt as @ references.");
        RequireContains(viewModel, "BuildSlashCommandSuggestions", "Typing / must list VSCodex commands, history, options handoff, and tool surfaces.");
        RequireContains(viewModel, "InsertPromptSuggestion", "Prompt suggestions must replace the active @/#// token instead of only appending text.");
        RequireContains(viewModel, "LastPromptTokenStart", "Prompt suggestions must know which token to replace.");
        RequireContains(viewModel, "\"/settings\"", "Slash suggestions must include settings.");
        RequireContains(viewModel, "\"/history\"", "Slash suggestions must include conversation history.");
        RequireContains(viewModel, "\"/mcp\"", "Slash suggestions must include MCP tools.");
        RequireContains(viewModel, "activeToken.StartsWith(\"#\"", "Context suggestions must include selected code and reference tokens.");
        RequireContains(viewModel, "ReviewSelectionCommand", "Context-sensitive code actions must be available in the tool-window view model.");
        RequireContains(viewModel, "NewThreadCommand", "The tool-window view model must support starting a new VSCodex thread.");
        RequireContains(codeBehind, "InsertPromptNewLine", "Ctrl+Enter must insert a newline in the prompt input.");
        RequireContains(codeBehind, "e.Key == Key.Enter", "Enter must run the active VSCodex prompt.");
        RequireContains(codeBehind, "ConversationScrollViewer.ScrollToEnd", "Conversation history must auto-scroll to newly added activity roots.");
        RequireContains(codeBehind, "Key.Tab", "Tab must insert the selected inline prompt suggestion.");
        RequireContains(codeBehind, "Key.Down", "Arrow keys must navigate inline prompt suggestions.");
        RequireContains(codeBehind, "InsertSelectedPromptSuggestion", "The prompt UI must insert the selected suggestion from keyboard or mouse.");
        RequireContains(codeBehind, "BrowseAndInsertFileReferences", "The prompt UI must turn the @ browse suggestion into a file picker.");
        RequireContains(codeBehind, "OpenFileDialog", "Disk-backed @ references must use a native Windows file picker.");
        RequireContains(codeBehind, "ClosePromptSuggestions", "Esc must close prompt suggestions before cancelling a run.");
        RequireContains(codeBehind, "OnPromptResizeDragDelta", "The prompt resize grip must be handled by the view.");
        RequireContains(codeBehind, "OnPromptResizeDragStarted", "Prompt resizing must initialize a bounded drag state.");
        RequireContains(codeBehind, "OnPromptResizeDragCompleted", "Prompt resize persistence must be deferred until the drag completes.");
        RequireContains(codeBehind, "FinishPromptResizeSafely", "Prompt resize event handlers must not let cleanup exceptions escape through Visual Studio.");
        RequireContains(codeBehind, "FinishPromptResize", "Prompt resizing must release mouse capture and clear cursor state on every exit path.");
        RequireContains(codeBehind, "ResetPromptResizeState", "Prompt resizing must have a non-throwing emergency cleanup path.");
        RequireContains(codeBehind, "Mouse.OverrideCursor = null", "Prompt resizing must clear the resize cursor when the drag ends or is interrupted.");
        RequireContains(codeBehind, "ReleaseMouseCapture", "Prompt resizing must explicitly release WPF mouse capture to avoid freezing Visual Studio in resize mode.");
        RequireContains(codeBehind, "ResolvePromptMaxHeight", "The prompt input maximum height must be based on available docked tool-window space.");
        RequireContains(codeBehind, "SetCurrentValue(HeightProperty, height)", "Prompt resizing must adjust the current text-box height without replacing the binding.");
        RequireContains(codeBehind, "Key.Escape", "Esc must cancel the active VSCodex request.");
        RequireContains(codeBehind, "OnCloseToolPanelClick", "The controls panel close button must update the view model.");
        RequireContains(codeBehind, "OnHistoryItemDoubleClick", "The history list must support double-click reopening.");
        RequireContains(codeBehind, "ApplyVisualStudioThemeToComboBoxes", "Code-behind must repair WPF editable combo-box template parts with Visual Studio theme resources.");
        RequireContains(codeBehind, "EnvironmentColors.ComboBoxTextBrushKey", "Code-behind must apply Visual Studio text resources to editable combo-box text boxes.");
        RequireContains(codeBehind, "VisualTreeHelper", "The combo-box theme repair must find generated controls after the WPF template is loaded.");
    }

    [Test]
    public void Vsix_project_deploys_to_experimental_instance_for_debugging()
    {
        var project = ReadText("src/VSCodex/VSCodex.csproj");
        var installerScript = ReadText("scripts/install-vsix-experimental.ps1");
        var launcherScript = ReadText("scripts/launch-vsix-installer.ps1");

        RequireContains(project, "<TargetFramework>net48</TargetFramework>", "The in-process AsyncPackage VSIX should target .NET Framework 4.8, not net472.");
        RequireDoesNotContain(project, "<TargetFramework>net8.0-windows", "Moving to net8 requires the out-of-process VisualStudio.Extensibility model, not a classic in-process VSPackage TFM swap.");
        RequireContains(project, "<VSSDKBuildToolsAutoSetup>true</VSSDKBuildToolsAutoSetup>", "VSIX project must use VSSDK build tools auto setup.");
        RequireContains(project, "<ProjectCapability Include=\"CreateVsixContainer\" />", "VSIX project must create a VSIX container.");
        RequireContains(project, "<PackageReference Include=\"Microsoft.VSSDK.BuildTools\" Version=\"18.5.40034\" />", "VSIX build tools must stay on the stable VS 18.5 toolset that can deploy locally.");
        RequireContains(project, "<DeployExtension>false</DeployExtension>", "The broken raw VSSDK local deploy target must stay disabled.");
        RequireContains(project, "<VSSDKTargetPlatformRegRootSuffix Condition=\"'$(VSSDKTargetPlatformRegRootSuffix)' == ''\">Exp</VSSDKTargetPlatformRegRootSuffix>", "VSIX debugging must target the Experimental hive.");
        RequireContains(project, "<DebuggerFlavor Condition=\"'$(Configuration)' == 'Debug'\">VsixDebugger</DebuggerFlavor>", "Debugging must use the VSIX debugger.");
        RequireContains(project, "InstallVSCodexVsixWithInstaller", "VSIX builds must have a shared VSIXInstaller deployment hook.");
        RequireContains(project, "DeployVSCodexVsixWithInstaller", "Command-line validation must be able to exercise the VSIXInstaller deployment hook.");
        RequireContains(project, "'$(DeployVSCodexVsixWithInstaller)' == 'true'", "Release builds must be able to launch VSIXInstaller when deployment is explicitly requested.");
        RequireContains(project, "'$(Configuration)' == 'Debug' and '$(BuildingInsideVisualStudio)' == 'true'", "Debug builds inside Visual Studio must still install the VSIX before launching the experimental instance.");
        RequireContains(project, "install-vsix-experimental.ps1", "VSIX deployment must use the VSIXInstaller-based script.");
        RequireContains(project, "CreateVSCodexVsixInstallerLauncher", "Release output must include a launcher that bypasses broken .vsix file associations.");
        RequireContains(project, "Install-VSCodex.cmd", "Release output must include a command launcher beside VSCodex.vsix.");
        RequireContains(project, "Install-VSCodex.ps1", "Release output must include a PowerShell launcher beside VSCodex.vsix.");
        RequireContains(project, "LaunchVSCodexVsixInstaller", "Command-line Release builds must be able to invoke the visible Visual Studio installer.");
        RequireContains(project, "VSCodexLaunchVsixInstaller", "Installer launch must be controlled by an explicit MSBuild property.");
        RequireContains(project, "IncludeVSCodexCommandTableInVsix", "The compiled VSCT command table must be packaged into the VSIX.");
        RequireContains(project, "<ResourceName>Menus.ctmenu</ResourceName>", "The VSCT resource name must match ProvideMenuResource(\"Menus.ctmenu\", 4).");
        RequireContains(project, "BeforeTargets=\"GetVsixSourceItems\"", "The compiled command table must be added before the VSIX source item list is collected.");
        RequireContains(project, "CodexCommands.cto", "The generated Codex command table must be packaged from the intermediate output.");
        RequireContains(project, "<TargetPath>Menus.ctmenu</TargetPath>", "The packaged command table must be installed at the VSIX root as Menus.ctmenu.");
        RequireDoesNotContain(project, "DestinationFiles=\"$(IntermediateOutputPath)Menus.ctmenu\"", "The command table must not depend on an after-the-fact copy that can miss VSIX item collection.");
        RequireContains(installerScript, "/rootSuffix:$RootSuffix", "VSIXInstaller must install into the requested Visual Studio root suffix.");
        RequireDoesNotContain(installerScript, "/force", "VSIXInstaller 18 can produce incomplete legacy installs with /force; stale copies must be removed before a normal install instead.");
        RequireContains(installerScript, "/instanceIds:$InstanceId", "VSIXInstaller must support targeting the current Visual Studio instance.");
        RequireContains(installerScript, "/UpdateConfiguration", "VSIXInstaller deployment must refresh the Visual Studio package cache after replacing extension folders.");
        RequireContains(installerScript, "extensions.configurationchanged", "VSIXInstaller deployment must mark the extension cache dirty before refreshing configuration.");
        RequireContains(installerScript, "Clear-VisualStudioExtensionCaches", "VSIXInstaller deployment must clear stale command and MEF caches before refreshing configuration.");
        RequireContains(installerScript, "Remove-StaleInstalledExtension", "Debug deployment must remove stale copies of the same VSIX ID before reinstalling.");
        RequireContains(installerScript, "Remove-StaleVSCodexPayloadDirectories", "Debug deployment must also remove incomplete VSCodex payload folders that no longer contain a manifest.");
        RequireContains(installerScript, "VSCodex.dll", "Debug deployment must identify stale manifest-less VSCodex payload directories by the extension assembly.");
        RequireContains(installerScript, "ExtensionMetadata*.mpack", "Debug deployment must clear Visual Studio 18 extension metadata caches so pkgdef command tables are rebuilt.");
        RequireContains(installerScript, "Where-Object { $_.Name -like \"ExtensionMetadata*.mpack\"", "Extension metadata cleanup must filter by filename before deleting files under the Visual Studio Extensions root.");
        RequireContains(installerScript, "Remove-VerifiedDirectory", "VSIXInstaller deployment cache cleanup must verify paths before recursive deletion.");
        RequireContains(installerScript, "PerUserEnabledExtensionsCache", "The installer script must wait for the extension to be enabled, not only copied.");
        RequireContains(launcherScript, "VSIXInstaller.exe", "The visible Release launcher must invoke Visual Studio VSIXInstaller directly.");
        RequireContains(launcherScript, "Start-Process", "The visible Release launcher must start the installer UI.");
        RequireContains(launcherScript, "ResolveOnly", "The visible Release launcher must support non-interactive validation.");
        RequireContains(launcherScript, "vswhere.exe", "The visible Release launcher must resolve Visual Studio instances through vswhere.");
        RequireContains(launcherScript, "foreach ($major in @('18', '2022'))", "The visible Release launcher must support Visual Studio 18 and 2022 installer paths.");
        RequireContains(launcherScript, "-like '*\\Microsoft Visual Studio\\*'", "The visible Release launcher must ignore VS-shell products that are not Visual Studio installs.");
        RequireContains(project, "<None Update=\"source.extension.vsixmanifest\">", "The source manifest must be a VSIX source manifest, not a packaged payload.");
        RequireDoesNotContain(project, "<Content Include=\"source.extension.vsixmanifest\"", "The source VSIX manifest must not be packaged as extension content.");
        RequireContains(project, "IncludeVSCodexRuntimeAssembliesInVsix", "Private runtime dependencies must be explicitly packaged in the VSIX.");
        RequireContains(project, "Newtonsoft.Json.dll", "Newtonsoft.Json must be packaged privately for the VSIX.");
        RequireContains(project, "System.Text.Json.dll", "System.Text.Json must be packaged privately for ReactiveUI runtime dependencies.");
        RequireContains(project, "Microsoft.Bcl.AsyncInterfaces.dll", "Microsoft.Bcl.AsyncInterfaces must be packaged privately for ReactiveUI.Extensions.");
        RequireContains(project, "System.Runtime.CompilerServices.Unsafe.dll", "Unsafe helpers must be packaged privately for ReactiveUI runtime dependencies.");
        RequireDoesNotExist("src/VSCodex/extension.vsixmanifest", "Generated extension.vsixmanifest must not be tracked beside the source manifest.");
        RequireDoesNotExist("src/VSCodex/merged.source.extension.vsixmanifest", "Generated merged source manifest must not be tracked beside the source manifest.");
    }

    private static void RequireGroupParent(XDocument document, string groupId, string expectedParentGuid, string expectedParentId)
    {
        var parent = document.Descendants(Vsct + "Group")
            .Where(group => (string?)group.Attribute("id") == groupId)
            .Elements(Vsct + "Parent")
            .SingleOrDefault();

        if (parent is null)
        {
            throw new InvalidOperationException($"Missing VSCT group '{groupId}'.");
        }

        if ((string?)parent.Attribute("guid") != expectedParentGuid || (string?)parent.Attribute("id") != expectedParentId)
        {
            throw new InvalidOperationException($"VSCT group '{groupId}' must be parented to {expectedParentGuid}/{expectedParentId}.");
        }
    }

    private static void RequireMenuParent(XDocument document, string menuId, string expectedParentGuid, string expectedParentId)
    {
        var parent = document.Descendants(Vsct + "Menu")
            .Where(menu => (string?)menu.Attribute("id") == menuId)
            .Elements(Vsct + "Parent")
            .SingleOrDefault();

        if (parent is null)
        {
            throw new InvalidOperationException($"Missing VSCT menu '{menuId}'.");
        }

        if ((string?)parent.Attribute("guid") != expectedParentGuid || (string?)parent.Attribute("id") != expectedParentId)
        {
            throw new InvalidOperationException($"VSCT menu '{menuId}' must be parented to {expectedParentGuid}/{expectedParentId}.");
        }
    }

    private static void RequireButtonParent(XDocument document, string buttonId, string expectedParentGuid, string expectedParentId)
    {
        var parent = document.Descendants(Vsct + "Button")
            .Where(button => (string?)button.Attribute("id") == buttonId)
            .Elements(Vsct + "Parent")
            .SingleOrDefault();

        if (parent is null)
        {
            throw new InvalidOperationException($"Missing VSCT button '{buttonId}'.");
        }

        if ((string?)parent.Attribute("guid") != expectedParentGuid || (string?)parent.Attribute("id") != expectedParentId)
        {
            throw new InvalidOperationException($"VSCT button '{buttonId}' must be parented to {expectedParentGuid}/{expectedParentId}.");
        }
    }

    private static void RequireButtonString(XDocument document, string buttonId, string elementName, string expectedValue)
    {
        var actual = document.Descendants(Vsct + "Button")
            .Where(button => (string?)button.Attribute("id") == buttonId)
            .Elements(Vsct + "Strings")
            .Elements(Vsct + elementName)
            .SingleOrDefault()
            ?.Value;

        if (!StringComparer.Ordinal.Equals(actual, expectedValue))
        {
            throw new InvalidOperationException($"VSCT button '{buttonId}' must have {elementName} '{expectedValue}', but found '{actual ?? "<missing>"}'.");
        }
    }

    private static void RequireButtonDoesNotHaveCommandFlag(XDocument document, string buttonId, string flagName)
    {
        var hasFlag = document.Descendants(Vsct + "Button")
            .Where(button => (string?)button.Attribute("id") == buttonId)
            .Elements(Vsct + "CommandFlag")
            .Any(flag => StringComparer.Ordinal.Equals(flag.Value, flagName));

        if (hasFlag)
        {
            throw new InvalidOperationException($"VSCT button '{buttonId}' must not use CommandFlag '{flagName}' because it must remain visible.");
        }
    }

    private static void RequireKeyBinding(XDocument document, string commandId, string expectedEditor, string expectedKey1, string expectedMod1)
    {
        var binding = document.Descendants(Vsct + "KeyBinding")
            .SingleOrDefault(element => (string?)element.Attribute("id") == commandId);

        if (binding is null)
        {
            throw new InvalidOperationException($"Missing key binding for command '{commandId}'.");
        }

        RequireAttribute(binding, "editor", expectedEditor);
        RequireAttribute(binding, "key1", expectedKey1);
        RequireAttribute(binding, "mod1", expectedMod1);
        RequireMissingAttribute(binding, "key2");
        RequireMissingAttribute(binding, "mod2");
    }

    private static void RequireAttribute(XElement element, string attributeName, string expectedValue)
    {
        var actual = (string?)element.Attribute(attributeName);
        if (!StringComparer.Ordinal.Equals(actual, expectedValue))
        {
            throw new InvalidOperationException($"Expected {element.Name.LocalName} attribute '{attributeName}' to be '{expectedValue}', but found '{actual ?? "<missing>"}'.");
        }
    }

    private static void RequireMissingAttribute(XElement element, string attributeName)
    {
        if (element.Attribute(attributeName) != null)
        {
            throw new InvalidOperationException($"Expected {element.Name.LocalName} attribute '{attributeName}' to be absent.");
        }
    }

    private static void RequireDoesNotDefineIdSymbol(XDocument document, string symbolName)
    {
        var exists = document.Descendants(Vsct + "IDSymbol").Any(symbol => (string?)symbol.Attribute("name") == symbolName);
        if (exists)
        {
            throw new InvalidOperationException($"VSCT must not redefine Visual Studio shell symbol '{symbolName}'; use the vsshlids.h Extern value instead.");
        }
    }

    private static void RequireCommandPlacement(XDocument document, string commandId, string expectedParentGuid, string expectedParentId)
    {
        var exists = document.Descendants(Vsct + "CommandPlacement")
            .Where(placement => (string?)placement.Attribute("id") == commandId)
            .Elements(Vsct + "Parent")
            .Any(parent => (string?)parent.Attribute("guid") == expectedParentGuid && (string?)parent.Attribute("id") == expectedParentId);

        if (!exists)
        {
            throw new InvalidOperationException($"Command '{commandId}' must be placed under {expectedParentGuid}/{expectedParentId}.");
        }
    }

    private static void RequireNoCommandPlacement(XDocument document, string commandId, string parentGuid, string parentId)
    {
        var exists = document.Descendants(Vsct + "CommandPlacement")
            .Where(placement => (string?)placement.Attribute("id") == commandId)
            .Elements(Vsct + "Parent")
            .Any(parent => (string?)parent.Attribute("guid") == parentGuid && (string?)parent.Attribute("id") == parentId);

        if (exists)
        {
            throw new InvalidOperationException($"Command '{commandId}' must not be placed under {parentGuid}/{parentId}; that placement creates duplicate Visual Studio context menu entries.");
        }
    }

    private static void RequireIdSymbol(XDocument document, string symbolName)
    {
        var exists = document.Descendants(Vsct + "IDSymbol").Any(symbol => (string?)symbol.Attribute("name") == symbolName);
        if (!exists)
        {
            throw new InvalidOperationException($"Missing VSCT IDSymbol '{symbolName}'.");
        }
    }

    private static void RequireVisibleCommandStringsUseVSCodex(XDocument document)
    {
        var badLabels = document.Descendants(Vsct + "Strings")
            .Elements()
            .Select(element => element.Value)
            .Where(value => value.Contains("Codex", StringComparison.Ordinal) && !value.Contains("VSCodex", StringComparison.Ordinal))
            .ToArray();

        if (badLabels.Length > 0)
        {
            throw new InvalidOperationException("Visible VSCT labels must use VSCodex, not Codex: " + string.Join(", ", badLabels));
        }
    }

    private static void RequireElementValue(XDocument document, string elementName, string expectedValue)
    {
        var actual = document.Descendants(elementName).SingleOrDefault()?.Value;
        if (!StringComparer.OrdinalIgnoreCase.Equals(actual, expectedValue))
        {
            throw new InvalidOperationException($"Expected {elementName} to be '{expectedValue}', but found '{actual ?? "<missing>"}'.");
        }
    }

    private static void RequirePackageReference(XDocument document, string packageId)
    {
        var exists = document.Descendants("PackageReference").Any(reference => (string?)reference.Attribute("Include") == packageId);
        if (!exists)
        {
            throw new InvalidOperationException($"Missing PackageReference '{packageId}'.");
        }
    }

    private static void RequireContains(string text, string expected, string message)
    {
        if (!text.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void RequireMatches(string text, string pattern, string message)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(text, pattern, System.Text.RegularExpressions.RegexOptions.Singleline))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void RequireDoesNotContain(string text, string unexpected, string message)
    {
        if (text.Contains(unexpected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void RequireDoesNotExist(string relativePath, string message)
    {
        if (File.Exists(PathFor(relativePath)))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void RequireExists(string relativePath, string message)
    {
        if (!File.Exists(PathFor(relativePath)))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static async Task<ProcessResult> RunProcessAsync(string fileName, string arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = RepositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new ProcessResult(-1, string.Empty, ex.ToString());
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, output, error);
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private static string ReadText(string relativePath) => File.ReadAllText(PathFor(relativePath));

    private static string PathFor(string relativePath) => Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string LocateRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "src", "VSCodex.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
