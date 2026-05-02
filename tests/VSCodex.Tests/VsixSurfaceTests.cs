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

        RequireMenuParent(vsct, "VSCodexTopLevelMenu", "guidCommandSet", "VSCodexExtensionsMenuGroup");
        RequireMenuParent(vsct, "VSCodexEditorContextMenu", "guidCommandSet", "VSCodexCodeWindowContextMenuGroup");
        RequireGroupParent(vsct, "VSCodexExtensionsMenuGroup", "guidSHLMainMenu", "IDM_VS_MENU_ADDINS");
        RequireGroupParent(vsct, "VSCodexTopLevelMenuGroup", "guidCommandSet", "VSCodexTopLevelMenu");
        RequireGroupParent(vsct, "CodexToolsMenuGroup", "guidSHLMainMenu", "IDM_VS_MENU_TOOLS");
        RequireGroupParent(vsct, "CodexViewMenuGroup", "guidSHLMainMenu", "IDM_VS_MENU_VIEW");
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

        RequireButtonParent(vsct, "OpenToolWindowCommandId", "guidSHLMainMenu", "IDG_VS_WNDO_OTRWNDWS1");
        RequireButtonParent(vsct, "OpenOptionsCommandId", "guidCommandSet", "VSCodexTopLevelMenuGroup");
        RequireButtonParent(vsct, "AskCodexCommandId", "guidCommandSet", "VSCodexEditorContextMenuActionsGroup");
        RequireButtonParent(vsct, "ExplainSelectionCommandId", "guidCommandSet", "VSCodexEditorContextMenuActionsGroup");
        RequireButtonParent(vsct, "FixSelectionCommandId", "guidCommandSet", "VSCodexEditorContextMenuActionsGroup");
        RequireButtonParent(vsct, "ReviewSelectionCommandId", "guidCommandSet", "VSCodexEditorContextMenuActionsGroup");
        RequireButtonParent(vsct, "OptimizeSelectionCommandId", "guidCommandSet", "VSCodexEditorContextMenuActionsGroup");
        RequireButtonParent(vsct, "GenerateDocsCommandId", "guidCommandSet", "VSCodexEditorContextMenuActionsGroup");
        RequireButtonParent(vsct, "CreateTestFromSelectionCommandId", "guidCommandSet", "VSCodexEditorContextMenuActionsGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "VSCodexTopLevelMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexToolsMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexViewMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexProjectContextMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexSolutionContextMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexItemContextMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexErrorListContextMenuGroup");
        RequireCommandPlacement(vsct, "VSCodexEditorContextMenu", "guidCommandSet", "VSCodexEditorAllContextMenuGroup");
        RequireCommandPlacement(vsct, "VSCodexEditorContextMenu", "guidCommandSet", "CodexEditorContextMenuGroup");
        RequireCommandPlacement(vsct, "VSCodexEditorContextMenu", "guidSHLMainMenu", "IDG_VS_CTXT_EDITOR_ALL");
        RequireCommandPlacement(vsct, "VSCodexEditorContextMenu", "guidSHLMainMenu", "IDG_VS_CODEWIN_DEBUG_STEP");
        RequireCommandPlacement(vsct, "VSCodexEditorContextMenu", "guidCommandSet", "CodexErrorCorrectionContextMenuGroup");
        RequireCommandPlacement(vsct, "VSCodexEditorContextMenu", "guidCommandSet", "CodexErrorListContextMenuGroup");
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
        RequireCommandPlacement(vsct, "DebugWithCodexCommandId", "guidSHLMainMenu", "IDG_VS_CODEWIN_DEBUG_STEP");
        RequireCommandPlacement(vsct, "DebugWithCodexCommandId", "guidSHLMainMenu", "IDG_VS_CTXT_ERROR_CORRECTION");
        RequireCommandPlacement(vsct, "DebugWithCodexCommandId", "guidSHLMainMenu", "IDG_VS_ERRORLIST");
        RequireCommandPlacement(vsct, "CreatePlanCommandId", "guidCommandSet", "CodexProjectContextMenuGroup");
        RequireCommandPlacement(vsct, "CreatePlanCommandId", "guidCommandSet", "CodexSolutionContextMenuGroup");
        RequireCommandPlacement(vsct, "CreatePlanCommandId", "guidCommandSet", "CodexItemContextMenuGroup");

        RequireIdSymbol(vsct, "VSCodexTopLevelMenu");
        RequireIdSymbol(vsct, "VSCodexExtensionsMenuGroup");
        RequireIdSymbol(vsct, "VSCodexTopLevelMenuGroup");
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
        RequireContains(commandSource, "QueryEditorContextCommandStatus", "Editor selection actions must be query-status aware.");
        RequireContains(commandSource, "QueryDebugCommandStatus", "Debug With VSCodex must adapt to runtime exception break mode.");
        RequireContains(commandSource, "Debug Exception with VSCodex", "Runtime exception break mode must show a specific VSCodex debug command label.");
    }

    [Test]
    public void Codex_defaults_include_failover_budget_analytics_and_ReactiveMemory_hooks()
    {
        var models = ReadText("src/VSCodex/Models/CodexModels.cs");
        var promptBuilder = ReadText("src/VSCodex/Services/PromptBuilder.cs");
        var mcpConfig = ReadText("src/VSCodex/Services/McpConfigService.cs");
        var view = ReadText("src/VSCodex/Views/VSCodexToolWindowControl.xaml");
        var analytics = ReadText("src/VSCodex/Services/ModelAnalyticsService.cs");

        RequireContains(models, "DefaultFailoverModel", "Settings must expose a failover model.");
        RequireContains(models, "gpt-5.5", "Primary model defaults must include the current flagship coding model.");
        RequireContains(models, "gpt-5.4-mini", "Budget defaults must include a cheaper model option.");
        RequireContains(promptBuilder, "reactivememory_status", "Prompt builder must inject ReactiveMemory session-start hooks.");
        RequireContains(promptBuilder, "reactivememory_react_to_prompt", "Prompt builder must inject per-prompt ReactiveMemory hooks.");
        RequireContains(mcpConfig, "[mcp_servers.reactivememory]", "MCP config service must install ReactiveMemory as the default memory server.");
        RequireContains(mcpConfig, "CP.ReactiveMemory.Mcp.Server", "MCP config service must know the ReactiveMemory package identity.");
        RequireContains(view, "Header=\"Analytics\"", "Tool window must expose model/cost analytics.");
        RequireContains(view, "FailoverModel", "Tool window must expose failover model control.");
        RequireContains(analytics, "EstimatedSavingsPercent", "Analytics must estimate whether a cheaper model can be used.");
    }

    [Test]
    public void Package_auto_loads_and_defers_first_run_tool_window_creation_until_after_initialization()
    {
        var packageSource = ReadText("src/VSCodex/VSCodexExtensionPackage.cs");
        var commandSource = ReadText("src/VSCodex/Commands/OpenVSCodexToolWindowCommand.cs");

        RequireContains(packageSource, "ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string", "Package must auto-load before a solution is open.");
        RequireContains(packageSource, "ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string", "Package must auto-load when a solution exists.");
        RequireContains(packageSource, "OpenVSCodexToolWindowCommand.InitializeAsync(this)", "Package initialization must register VSCodex commands.");
        RequireContains(packageSource, "ScheduleShowToolWindowOnFirstLaunch();", "First-run tool-window launch must be scheduled after command registration.");
        RequireContains(packageSource, "JoinableTaskFactory.RunAsync", "First-run tool-window launch must not block package initialization.");
        RequireContains(packageSource, "Task.Delay(TimeSpan.FromMilliseconds(1500)", "First-run tool-window launch must wait for the shell to settle.");
        RequireContains(packageSource, "FirstLaunchToolWindowOpened", "First-run state must be persisted so user layout is respected thereafter.");
        RequireContains(packageSource, "FirstLaunchToolWindowOpenedV7", "The first-run key must advance when the tool-window launch behavior changes.");
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
    public void VSCodex_settings_are_hosted_in_the_tool_window_not_legacy_Tools_Options()
    {
        var packageSource = ReadText("src/VSCodex/VSCodexExtensionPackage.cs");
        var commandSource = ReadText("src/VSCodex/Commands/OpenVSCodexToolWindowCommand.cs");
        var toolWindow = ReadText("src/VSCodex/ToolWindows/VSCodexToolWindowPane.cs");
        var viewModel = ReadText("src/VSCodex/ViewModels/VSCodexToolWindowViewModel.cs");
        var view = ReadText("src/VSCodex/Views/VSCodexToolWindowControl.xaml");
        var manifest = ReadText("src/VSCodex/source.extension.vsixmanifest");

        RequireDoesNotContain(packageSource, "ProvideOptionPage", "VSCodex settings must not register a legacy DialogPage.");
        RequireDoesNotContain(packageSource, "ProvideProfile", "VSCodex settings must not register a legacy profile page.");
        RequireDoesNotExist("src/VSCodex/Options/CodexOptionsPage.cs", "The legacy DialogPage implementation must not ship with VSCodex.");
        RequireContains(commandSource, "OpenToolWindowAsync(window => window.ShowSettings())", "The settings command must open the VSCodex tool-window settings surface.");
        RequireContains(toolWindow, "Caption = \"VSCodex\"", "The tool-window caption must be VSCodex.");
        RequireContains(toolWindow, "ShowSettingsAsync", "The tool window must expose an explicit settings launch path.");
        RequireContains(viewModel, "SelectedToolTabIndex", "The settings launch path must be able to select the settings tab.");
        RequireContains(viewModel, "Status = \"VSCodex settings\"", "Opening settings must update status with the VSCodex name.");
        RequireContains(view, "SelectedIndex=\"{Binding SelectedToolTabIndex, Mode=TwoWay}\"", "The settings tab selection must be bound.");
        RequireContains(view, "Header=\"Settings\"", "The model and execution controls must be hosted as the VSCodex settings tab.");
        RequireContains(manifest, "<DisplayName>VSCodex</DisplayName>", "The VSIX display name must be VSCodex.");
        RequireContains(manifest, "Version=\"0.1.12\"", "The VSIX version must change so Visual Studio updates the installed experimental extension.");
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
        RequireContains(workflow, "microsoft/setup-msbuild@v2", "The Marketplace workflow must build with MSBuild on Windows.");
        RequireContains(workflow, "actions/upload-artifact@v4", "The Marketplace workflow must publish the built VSIX as an artifact.");
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
        RequireContains(orchestrator, "SDK failure:", "CLI fallback failures must preserve the original SDK bridge failure.");
        RequireContains(orchestrator, "npm install -g @openai/codex-sdk", "Combined run failure must direct the user to install the Codex SDK.");
        RequireContains(orchestrator, "%USERPROFILE%\\\\.codex\\\\config.toml", "Combined run failure must point users to the Codex profile configuration when profile selection breaks fallback.");
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
        RequireContains(viewModel, "EnsureCodexSdkReadyForRunAsync", "Run must be blocked with guidance when the SDK is missing.");
        RequireContains(viewModel, "CheckPrerequisitesAsync", "Startup must check VSCodex prerequisites.");
        RequireContains(view, "Check setup", "The settings surface must let the user rerun prerequisite checks.");
        RequireContains(view, "CodexSetupInstructions", "The settings surface must show Windows setup instructions.");
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

        RequireContains(view, "MinWidth=\"320\"", "The tool window must declare a practical minimum width for docked narrow layouts.");
        RequireContains(view, "MaxWidth=\"{Binding ActualWidth, ElementName=Root}\"", "The settings panel must be constrained to the actual tool-window width.");
        RequireContains(view, "Panel.ZIndex=\"10\"", "The settings panel must overlay the conversation in narrow layouts instead of disappearing off-screen.");
        RequireContains(view, "<WrapPanel Grid.Row=\"2\"", "The prompt footer must wrap model controls and run buttons instead of forcing fixed columns.");
        RequireContains(view, "Header=\"Settings\"", "Execution settings must be grouped in a dedicated tab instead of a single overflowing toolbar.");
        RequireContains(view, "Rate limits remaining", "The tool window must surface hourly and weekly rate-limit details near the model controls.");
        RequireContains(view, "New Thread", "The tool window must expose a first-class new thread action.");
        RequireContains(view, "BooleanToVisibilityConverter", "The controls tab strip must be hidden until the user opens it.");
        RequireContains(view, "IsSettingsPanelOpen", "The controls tab strip must not occupy workspace permanently.");
        RequireContains(view, "CanEditSettings", "Model and settings controls must be locked while a task is running.");
        RequireContains(view, "OnCloseSettingsPanelClick", "The controls panel must have an explicit close action.");
        RequireContains(view, "PreviewKeyDown=\"OnPromptPreviewKeyDown\"", "The prompt box must support keyboard shortcuts.");
        RequireContains(view, "Ctrl+Enter", "The run shortcut must be visible in the prompt UI.");
        RequireContains(view, "PromptSuggestionPopup", "The prompt box must show inline VSCodex suggestions for /, @, and # tokens.");
        RequireContains(view, "ItemsSource=\"{Binding PromptSuggestions}\"", "Inline prompt suggestions must be backed by the view-model suggestion list.");
        RequireContains(view, "IsOpen=\"{Binding IsPromptSuggestionOpen, Mode=TwoWay}\"", "The prompt suggestion popup must be controlled by view-model state.");
        RequireContains(view, "OnPromptSuggestionDoubleClick", "Prompt suggestions must be insertable with the mouse.");
        RequireContains(view, "Header=\"Context\"", "Context-sensitive file and selection references must be grouped in the tool pane.");
        RequireContains(view, "FileTabSelectedBackgroundBrushKey", "Selected settings tabs must use Visual Studio file-tab selected background resources for contrast.");
        RequireContains(view, "FileTabSelectedTextBrushKey", "Selected settings tabs must use Visual Studio file-tab selected text resources for contrast.");
        RequireContains(view, "ContentSource=\"Header\"", "Tab headers must be rendered by the themed template so selected text remains readable.");
        RequireContains(view, "TargetType=\"ComboBox\"", "Combo boxes must receive explicit theme styling.");
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
        RequireContains(view, "TargetType=\"GroupBox\"", "Grouped panes must receive explicit theme styling.");
        RequireContains(view, "EnvironmentColors.ToolWindowBackgroundBrushKey", "Tool-window backgrounds must use Visual Studio theme brushes.");
        RequireContains(view, "EnvironmentColors.ToolWindowTextBrushKey", "Tool-window foregrounds must use Visual Studio theme brushes.");
        RequireContains(view, "EnvironmentColors.ToolWindowBorderBrushKey", "Tool-window borders must use Visual Studio theme brushes.");
        RequireContains(view, "SystemColors.HighlightTextBrushKey", "Selected list items must use system highlight text for contrast.");
        RequireContains(view, "MessageTextBoxStyle", "Read-only chat message text must have its own themed style.");
        RequireContains(viewModel, "RateLimits", "Rate-limit rows must be backed by view-model state.");
        RequireContains(viewModel, "UpdateRateLimitsFromJson", "SDK rate-limit telemetry must update the visible rate-limit rows.");
        RequireContains(viewModel, "Label = \"5h\"", "Rate-limit UI must expose the Codex five-hour window rather than a one-hour label.");
        RequireContains(viewModel, "FindRateLimitToken", "Rate-limit parsing must accept explicit remaining/reset telemetry when the SDK emits it.");
        RequireContains(viewModel, "\"primary\"", "Rate-limit parsing must understand the Codex primary five-hour telemetry window.");
        RequireContains(viewModel, "\"secondary\"", "Rate-limit parsing must understand the Codex secondary weekly telemetry window.");
        RequireContains(viewModel, "used_percent", "Rate-limit parsing must consume Codex used-percent telemetry.");
        RequireContains(viewModel, "100 - usedPercent.Value", "Codex used-percent telemetry must be converted to the remaining percentage shown by Codex.");
        RequireContains(viewModel, "FormatRateLimitReset", "Rate-limit reset times must be formatted to match the Codex UI.");
        RequireDoesNotContain(viewModel, "FindUsageToken", "Rate-limit rows must not fall back to synthetic usage-token estimates.");
        RequireDoesNotContain(viewModel, "UpdateObservedUsageFromToken", "Rate-limit rows must not show observed token estimates as real Codex limits.");
        RequireContains(viewModel, "result.usage", "SDK bridge result usage must be parsed from the current response shape.");
        RequireDoesNotContain(viewModel, "used \" + FormatTokenCount", "Observed token usage must not be surfaced as the real Codex rate-limit value.");
        RequireContains(viewModel, "CanEditSettings => !IsRunning", "Settings must not be editable while a task is running.");
        RequireContains(viewModel, "CanChangeSetting", "The view model must reject setting changes even if a delayed binding fires while a task is running.");
        RequireContains(viewModel, "VSCodex settings are locked while a task is running", "Blocked setting changes must produce visible user feedback.");
        RequireContains(viewModel, "IsSettingsPanelOpen = true", "The settings command must open the controls panel on demand.");
        RequireContains(viewModel, "PromptSuggestions", "The view model must expose inline prompt suggestions.");
        RequireContains(viewModel, "UpdatePromptSuggestions", "Typing /, @, or # must update context-sensitive prompt suggestions.");
        RequireContains(viewModel, "TargetTab = \"browse-files\"", "Typing @ must offer a disk file picker for references outside the repository.");
        RequireContains(viewModel, "InsertFileReferencePaths", "Files selected from disk must be inserted into the prompt as @ references.");
        RequireContains(viewModel, "BuildSlashCommandSuggestions", "Typing / must list VSCodex commands, settings, and tool surfaces.");
        RequireContains(viewModel, "InsertPromptSuggestion", "Prompt suggestions must replace the active @/#// token instead of only appending text.");
        RequireContains(viewModel, "LastPromptTokenStart", "Prompt suggestions must know which token to replace.");
        RequireContains(viewModel, "\"/settings\"", "Slash suggestions must include settings.");
        RequireContains(viewModel, "\"/mcp\"", "Slash suggestions must include MCP tools.");
        RequireContains(viewModel, "activeToken.StartsWith(\"#\"", "Context suggestions must include selected code and reference tokens.");
        RequireContains(viewModel, "ReviewSelectionCommand", "Context-sensitive code actions must be available in the tool-window view model.");
        RequireContains(viewModel, "NewThreadCommand", "The tool-window view model must support starting a new VSCodex thread.");
        RequireContains(codeBehind, "Keyboard.Modifiers.HasFlag(ModifierKeys.Control)", "Ctrl+Enter must run the active VSCodex prompt.");
        RequireContains(codeBehind, "Key.Tab", "Tab must insert the selected inline prompt suggestion.");
        RequireContains(codeBehind, "Key.Down", "Arrow keys must navigate inline prompt suggestions.");
        RequireContains(codeBehind, "InsertSelectedPromptSuggestion", "The prompt UI must insert the selected suggestion from keyboard or mouse.");
        RequireContains(codeBehind, "BrowseAndInsertFileReferences", "The prompt UI must turn the @ browse suggestion into a file picker.");
        RequireContains(codeBehind, "OpenFileDialog", "Disk-backed @ references must use a native Windows file picker.");
        RequireContains(codeBehind, "ClosePromptSuggestions", "Esc must close prompt suggestions before cancelling a run.");
        RequireContains(codeBehind, "Key.Escape", "Esc must cancel the active VSCodex request.");
        RequireContains(codeBehind, "OnCloseSettingsPanelClick", "The controls panel close button must update the view model.");
        RequireContains(codeBehind, "ApplyVisualStudioThemeToComboBoxes", "Code-behind must repair WPF editable combo-box template parts with Visual Studio theme resources.");
        RequireContains(codeBehind, "EnvironmentColors.ComboBoxTextBrushKey", "Code-behind must apply Visual Studio text resources to editable combo-box text boxes.");
        RequireContains(codeBehind, "VisualTreeHelper", "The combo-box theme repair must find generated controls after the WPF template is loaded.");
    }

    [Test]
    public void Vsix_project_deploys_to_experimental_instance_for_debugging()
    {
        var project = ReadText("src/VSCodex/VSCodex.csproj");
        var installerScript = ReadText("scripts/install-vsix-experimental.ps1");

        RequireContains(project, "<TargetFramework>net48</TargetFramework>", "The in-process AsyncPackage VSIX should target .NET Framework 4.8, not net472.");
        RequireDoesNotContain(project, "<TargetFramework>net8.0-windows", "Moving to net8 requires the out-of-process VisualStudio.Extensibility model, not a classic in-process VSPackage TFM swap.");
        RequireContains(project, "<VSSDKBuildToolsAutoSetup>true</VSSDKBuildToolsAutoSetup>", "VSIX project must use VSSDK build tools auto setup.");
        RequireContains(project, "<ProjectCapability Include=\"CreateVsixContainer\" />", "VSIX project must create a VSIX container.");
        RequireContains(project, "<PackageReference Include=\"Microsoft.VSSDK.BuildTools\" Version=\"18.5.40034\" />", "VSIX build tools must stay on the stable VS 18.5 toolset that can deploy locally.");
        RequireContains(project, "<DeployExtension>false</DeployExtension>", "The broken raw VSSDK local deploy target must stay disabled.");
        RequireContains(project, "<VSSDKTargetPlatformRegRootSuffix Condition=\"'$(VSSDKTargetPlatformRegRootSuffix)' == ''\">Exp</VSSDKTargetPlatformRegRootSuffix>", "VSIX debugging must target the Experimental hive.");
        RequireContains(project, "<DebuggerFlavor Condition=\"'$(Configuration)' == 'Debug'\">VsixDebugger</DebuggerFlavor>", "Debugging must use the VSIX debugger.");
        RequireContains(project, "InstallVSCodexVsixForDebugging", "Debug builds must install the VSIX before launching the experimental instance.");
        RequireContains(project, "DeployVSCodexVsixWithInstaller", "Command-line validation must be able to exercise the VSIXInstaller deployment hook.");
        RequireContains(project, "install-vsix-experimental.ps1", "Debug deployment must use the VSIXInstaller-based script.");
        RequireContains(project, "IncludeVSCodexCommandTableInVsix", "The compiled VSCT command table must be packaged into the VSIX.");
        RequireContains(project, "CodexCommands.cto", "The generated Codex command table must be copied from the intermediate output.");
        RequireContains(project, "Menus.ctmenu", "The packaged command table must match ProvideMenuResource(\"Menus.ctmenu\", 1).");
        RequireContains(installerScript, "/rootSuffix:$RootSuffix", "VSIXInstaller must install into the requested Visual Studio root suffix.");
        RequireContains(installerScript, "/instanceIds:$InstanceId", "VSIXInstaller must support targeting the current Visual Studio instance.");
        RequireContains(installerScript, "PerUserEnabledExtensionsCache", "The installer script must wait for the extension to be enabled, not only copied.");
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
