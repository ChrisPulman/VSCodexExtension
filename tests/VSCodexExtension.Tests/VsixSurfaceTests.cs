using System.Xml.Linq;
using TUnit.Core;

namespace VSCodexExtension.Tests;

public sealed class VsixSurfaceTests
{
    private static readonly XNamespace Vsct = "http://schemas.microsoft.com/VisualStudio/2005-10-18/CommandTable";
    private static readonly string RepositoryRoot = LocateRepositoryRoot();

    [Test]
    public void Solution_includes_TUnit_MTP_test_project()
    {
        var slnx = ReadText("src/VSCodexExtension.slnx");
        RequireContains(slnx, "../tests/VSCodexExtension.Tests/VSCodexExtension.Tests.csproj", "Solution must include the TUnit/MTP regression test project.");

        var testProject = XDocument.Load(PathFor("tests/VSCodexExtension.Tests/VSCodexExtension.Tests.csproj"));
        RequireElementValue(testProject, "UseMicrosoftTestingPlatformRunner", "true");
        RequirePackageReference(testProject, "TUnit");
        RequirePackageReference(testProject, "Microsoft.Testing.Platform");
    }

    [Test]
    public void VSCodex_tool_window_is_available_from_Extensions_View_Tools_Debug_and_context_menus()
    {
        var vsct = XDocument.Load(PathFor("src/VSCodexExtension/Commands/CodexCommands.vsct"));

        RequireMenuParent(vsct, "VSCodexTopLevelMenu", "guidCommandSet", "VSCodexExtensionsMenuGroup");
        RequireGroupParent(vsct, "VSCodexExtensionsMenuGroup", "guidSHLMainMenu", "IDM_VS_MENU_ADDINS");
        RequireGroupParent(vsct, "VSCodexTopLevelMenuGroup", "guidCommandSet", "VSCodexTopLevelMenu");
        RequireGroupParent(vsct, "CodexToolsMenuGroup", "guidSHLMainMenu", "IDM_VS_MENU_TOOLS");
        RequireGroupParent(vsct, "CodexViewMenuGroup", "guidSHLMainMenu", "IDM_VS_MENU_VIEW");
        RequireGroupParent(vsct, "CodexProjectContextMenuGroup", "guidSHLMainMenu", "IDM_VS_CTXT_PROJNODE");
        RequireGroupParent(vsct, "CodexSolutionContextMenuGroup", "guidSHLMainMenu", "IDM_VS_CTXT_SOLNNODE");
        RequireGroupParent(vsct, "CodexItemContextMenuGroup", "guidSHLMainMenu", "IDM_VS_CTXT_ITEMNODE");
        RequireGroupParent(vsct, "CodexEditorContextMenuGroup", "guidSHLMainMenu", "IDM_VS_CTXT_CODEWIN");
        RequireGroupParent(vsct, "CodexDebugMenuGroup", "guidSHLMainMenu", "IDM_VS_MENU_DEBUG");
        RequireGroupParent(vsct, "CodexErrorListContextMenuGroup", "guidSHLMainMenu", "IDM_VS_CTXT_ERRORLIST");
        RequireGroupParent(vsct, "CodexErrorCorrectionContextMenuGroup", "guidSHLMainMenu", "IDM_VS_CTXT_ERROR_CORRECTION");

        RequireButtonParent(vsct, "OpenToolWindowCommandId", "guidSHLMainMenu", "IDG_VS_WNDO_OTRWNDWS1");
        RequireButtonParent(vsct, "OpenOptionsCommandId", "guidCommandSet", "VSCodexTopLevelMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "VSCodexTopLevelMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexToolsMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexViewMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexProjectContextMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexSolutionContextMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexItemContextMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexEditorContextMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexErrorListContextMenuGroup");
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
        RequireVisibleCommandStringsUseVSCodex(vsct);
    }

    [Test]
    public void Codex_defaults_include_failover_budget_analytics_and_ReactiveMemory_hooks()
    {
        var models = ReadText("src/VSCodexExtension/Models/CodexModels.cs");
        var promptBuilder = ReadText("src/VSCodexExtension/Services/PromptBuilder.cs");
        var mcpConfig = ReadText("src/VSCodexExtension/Services/McpConfigService.cs");
        var view = ReadText("src/VSCodexExtension/Views/VSCodexToolWindowControl.xaml");
        var analytics = ReadText("src/VSCodexExtension/Services/ModelAnalyticsService.cs");

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
        var packageSource = ReadText("src/VSCodexExtension/VSCodexExtensionPackage.cs");
        var commandSource = ReadText("src/VSCodexExtension/Commands/OpenVSCodexToolWindowCommand.cs");

        RequireContains(packageSource, "ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string", "Package must auto-load before a solution is open.");
        RequireContains(packageSource, "ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string", "Package must auto-load when a solution exists.");
        RequireContains(packageSource, "OpenVSCodexToolWindowCommand.InitializeAsync(this)", "Package initialization must register VSCodex commands.");
        RequireContains(packageSource, "ScheduleShowToolWindowOnFirstLaunch();", "First-run tool-window launch must be scheduled after command registration.");
        RequireContains(packageSource, "JoinableTaskFactory.RunAsync", "First-run tool-window launch must not block package initialization.");
        RequireContains(packageSource, "Task.Delay(TimeSpan.FromMilliseconds(1500)", "First-run tool-window launch must wait for the shell to settle.");
        RequireContains(packageSource, "FirstLaunchToolWindowOpened", "First-run state must be persisted so user layout is respected thereafter.");
        RequireContains(packageSource, "FirstLaunchToolWindowOpenedV5", "The first-run key must advance when the tool-window launch behavior changes.");
        RequireContains(packageSource, "ShowToolWindowAsync(typeof(VSCodexToolWindowPane)", "The package must still show the VSCodex tool window on first run.");
        RequireDoesNotContain(packageSource, "await ShowToolWindowOnFirstLaunchAsync(cancellationToken)", "Package initialization must not synchronously await WPF tool-window creation.");
        RequireContains(commandSource, "ShowToolWindowAsync(typeof(VSCodexToolWindowPane)", "The VSCodex tool window must still open through the explicit command path.");
    }

    [Test]
    public void Tool_window_startup_failures_leave_a_visible_VSCodex_diagnostic_surface()
    {
        var toolWindow = ReadText("src/VSCodexExtension/ToolWindows/VSCodexToolWindowPane.cs");
        var fallback = ReadText("src/VSCodexExtension/Controls/VSCodexToolWindowFallbackControl.cs");
        var commandSource = ReadText("src/VSCodexExtension/Commands/OpenVSCodexToolWindowCommand.cs");

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
        var packageSource = ReadText("src/VSCodexExtension/VSCodexExtensionPackage.cs");
        var commandSource = ReadText("src/VSCodexExtension/Commands/OpenVSCodexToolWindowCommand.cs");
        var toolWindow = ReadText("src/VSCodexExtension/ToolWindows/VSCodexToolWindowPane.cs");
        var viewModel = ReadText("src/VSCodexExtension/ViewModels/VSCodexToolWindowViewModel.cs");
        var view = ReadText("src/VSCodexExtension/Views/VSCodexToolWindowControl.xaml");
        var manifest = ReadText("src/VSCodexExtension/source.extension.vsixmanifest");

        RequireDoesNotContain(packageSource, "ProvideOptionPage", "VSCodex settings must not register a legacy DialogPage.");
        RequireDoesNotContain(packageSource, "ProvideProfile", "VSCodex settings must not register a legacy profile page.");
        RequireDoesNotExist("src/VSCodexExtension/Options/CodexOptionsPage.cs", "The legacy DialogPage implementation must not ship with VSCodex.");
        RequireContains(commandSource, "OpenToolWindowAsync(window => window.ShowSettings())", "The settings command must open the VSCodex tool-window settings surface.");
        RequireContains(toolWindow, "Caption = \"VSCodex\"", "The tool-window caption must be VSCodex.");
        RequireContains(toolWindow, "ShowSettingsAsync", "The tool window must expose an explicit settings launch path.");
        RequireContains(viewModel, "SelectedToolTabIndex", "The settings launch path must be able to select the settings tab.");
        RequireContains(viewModel, "Status = \"VSCodex settings\"", "Opening settings must update status with the VSCodex name.");
        RequireContains(view, "SelectedIndex=\"{Binding SelectedToolTabIndex, Mode=TwoWay}\"", "The settings tab selection must be bound.");
        RequireContains(view, "Header=\"Settings\"", "The model and execution controls must be hosted as the VSCodex settings tab.");
        RequireContains(manifest, "<DisplayName>VSCodex</DisplayName>", "The VSIX display name must be VSCodex.");
        RequireContains(manifest, "Version=\"0.1.4\"", "The VSIX version must change so Visual Studio updates the installed experimental extension.");
    }

    [Test]
    public void Prompt_context_updates_are_marshalled_to_the_Visual_Studio_UI_thread()
    {
        var viewModel = ReadText("src/VSCodexExtension/ViewModels/VSCodexToolWindowViewModel.cs");
        var workspaceService = ReadText("src/VSCodexExtension/Services/WorkspaceContextService.cs");

        RequireContains(workspaceService, "SearchContextReferences(string query, int limit)", "Workspace service must expose context reference search.");
        RequireContains(workspaceService, "ThreadHelper.ThrowIfNotOnUIThread();", "DTE-backed workspace calls must keep their UI-thread guard.");
        RequireContains(viewModel, "JoinableTaskFactory joinableTaskFactory", "The tool-window view model must receive the Visual Studio joinable task factory.");
        RequireContains(viewModel, "_joinableTaskFactory.RunAsync", "Prompt changes must marshal throttled work through the Visual Studio joinable task factory.");
        RequireContains(viewModel, "await _joinableTaskFactory.SwitchToMainThreadAsync();", "Prompt context updates must switch to the Visual Studio UI thread before reading DTE state.");
        RequireContains(viewModel, "Volatile.Read(ref _promptChangeRevision)", "Stale throttled prompt updates must be ignored after marshaling to the UI thread.");
        RequireContains(viewModel, "UpdateReferenceSuggestions(prompt);", "Reference suggestions must still be refreshed after the UI-thread switch.");
    }

    [Test]
    public void Codex_transport_avoids_Visual_Studio_Newtonsoft_binding_breaks_and_reports_missing_executables()
    {
        var sdkClient = ReadText("src/VSCodexExtension/Services/CodexSdkJsonClient.cs");
        var cliClient = ReadText("src/VSCodexExtension/Services/CodexCliClient.cs");
        var orchestrator = ReadText("src/VSCodexExtension/Services/CodexOrchestrator.cs");

        RequireContains(sdkClient, "ToCompactJson", "SDK bridge JSON must use a helper that avoids Visual Studio binding-sensitive Newtonsoft overloads.");
        RequireContains(sdkClient, "JsonConvert.SerializeObject(token)", "SDK bridge JSON serialization must not require JToken.ToString(Formatting).");
        RequireDoesNotContain(sdkClient, "Formatting.None", "SDK bridge JSON must avoid JToken.ToString(Formatting.None), which can bind to older Visual Studio Newtonsoft.Json assemblies.");
        RequireContains(sdkClient, "Node.js executable was not found", "Missing node.exe must produce an actionable VSCodex error.");
        RequireContains(sdkClient, "winget install OpenJS.NodeJS.LTS", "Missing node.exe guidance must include Windows install instructions.");
        RequireContains(cliClient, "Codex CLI executable was not found", "Missing codex.exe must produce an actionable VSCodex error.");
        RequireContains(cliClient, "npm install -g @openai/codex", "Missing Codex CLI guidance must include the Windows npm install command.");
        RequireContains(cliClient, "Win32Exception", "Process start failures must be translated from raw Win32 exceptions.");
        RequireContains(orchestrator, "SDK failure:", "CLI fallback failures must preserve the original SDK bridge failure.");
        RequireContains(orchestrator, "npm install -g @openai/codex-sdk", "Combined run failure must direct the user to install the Codex SDK.");
    }

    [Test]
    public void Tool_window_checks_Codex_SDK_prerequisites_on_startup_and_shows_Windows_setup()
    {
        var bridge = ReadText("src/VSCodexExtension/Resources/codex-bridge.mjs");
        var environmentService = ReadText("src/VSCodexExtension/Services/CodexEnvironmentService.cs");
        var appBuilder = ReadText("src/VSCodexExtension/Infrastructure/RxAppBuilder.cs");
        var viewModel = ReadText("src/VSCodexExtension/ViewModels/VSCodexToolWindowViewModel.cs");
        var view = ReadText("src/VSCodexExtension/Views/VSCodexToolWindowControl.xaml");
        var models = ReadText("src/VSCodexExtension/Models/CodexModels.cs");

        RequireContains(bridge, "process.argv.includes('--check')", "The Node bridge must expose a startup health check path.");
        RequireContains(bridge, "npm install -g @openai/codex-sdk", "Bridge health check failures must include the SDK install command.");
        RequireContains(environmentService, "ICodexEnvironmentService", "VSCodex must have a dedicated Codex environment/prerequisite service.");
        RequireContains(environmentService, "CheckCodexSdkAsync", "Startup checks must verify that @openai/codex-sdk is importable.");
        RequireContains(environmentService, "winget install OpenJS.NodeJS.LTS", "Windows setup must explain how to install Node.js.");
        RequireContains(environmentService, "npm install -g @openai/codex-sdk", "Windows setup must explain how to install the Codex SDK.");
        RequireContains(environmentService, "npm install -g @openai/codex", "Windows setup must explain the optional CLI fallback install.");
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
    public void Tool_window_streaming_events_and_collections_are_marshaled_to_the_WPF_dispatcher()
    {
        var viewModel = ReadText("src/VSCodexExtension/ViewModels/VSCodexToolWindowViewModel.cs");

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
        var view = ReadText("src/VSCodexExtension/Views/VSCodexToolWindowControl.xaml");
        var viewModel = ReadText("src/VSCodexExtension/ViewModels/VSCodexToolWindowViewModel.cs");
        var codeBehind = ReadText("src/VSCodexExtension/Views/VSCodexToolWindowControl.xaml.cs");

        RequireContains(view, "GridSplitter", "The conversation and control panes must be resizable instead of fixed to a cramped layout.");
        RequireContains(view, "Header=\"Settings\"", "Execution settings must be grouped in a dedicated tab instead of a single overflowing toolbar.");
        RequireContains(view, "Rate limits remaining", "The tool window must surface hourly and weekly rate-limit details near the model controls.");
        RequireContains(view, "New Thread", "The tool window must expose a first-class new thread action.");
        RequireContains(view, "PreviewKeyDown=\"OnPromptPreviewKeyDown\"", "The prompt box must support keyboard shortcuts.");
        RequireContains(view, "Ctrl+Enter", "The run shortcut must be visible in the prompt UI.");
        RequireContains(view, "Header=\"Context\"", "Context-sensitive file and selection references must be grouped in the tool pane.");
        RequireContains(view, "TargetType=\"ComboBox\"", "Combo boxes must receive explicit theme styling.");
        RequireContains(view, "SystemColors.WindowTextBrushKey", "Combo box selected text must use a foreground that is visible on the native edit surface.");
        RequireContains(view, "SystemColors.WindowBrushKey", "Combo box selected text must use a background that matches the native edit surface.");
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
        RequireContains(viewModel, "ReviewSelectionCommand", "Context-sensitive code actions must be available in the tool-window view model.");
        RequireContains(viewModel, "NewThreadCommand", "The tool-window view model must support starting a new VSCodex thread.");
        RequireContains(codeBehind, "Keyboard.Modifiers.HasFlag(ModifierKeys.Control)", "Ctrl+Enter must run the active VSCodex prompt.");
        RequireContains(codeBehind, "Key.Escape", "Esc must cancel the active VSCodex request.");
    }

    [Test]
    public void Vsix_project_deploys_to_experimental_instance_for_debugging()
    {
        var project = ReadText("src/VSCodexExtension/VSCodexExtension.csproj");
        var installerScript = ReadText("scripts/install-vsix-experimental.ps1");

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
        RequireDoesNotExist("src/VSCodexExtension/extension.vsixmanifest", "Generated extension.vsixmanifest must not be tracked beside the source manifest.");
        RequireDoesNotExist("src/VSCodexExtension/merged.source.extension.vsixmanifest", "Generated merged source manifest must not be tracked beside the source manifest.");
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

    private static string ReadText(string relativePath) => File.ReadAllText(PathFor(relativePath));

    private static string PathFor(string relativePath) => Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string LocateRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "src", "VSCodexExtension.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }
}
