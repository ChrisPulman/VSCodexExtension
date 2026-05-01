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
    public void Codex_tool_window_is_available_from_View_Project_Solution_and_code_selection_contexts()
    {
        var vsct = XDocument.Load(PathFor("src/VSCodexExtension/Commands/CodexCommands.vsct"));

        RequireGroupParent(vsct, "CodexViewMenuGroup", "guidSHLMainMenu", "IDM_VS_MENU_VIEW");
        RequireGroupParent(vsct, "CodexProjectContextMenuGroup", "guidSHLMainMenu", "IDM_VS_CTXT_PROJNODE");
        RequireGroupParent(vsct, "CodexSolutionContextMenuGroup", "guidSHLMainMenu", "IDM_VS_CTXT_SOLNNODE");
        RequireGroupParent(vsct, "CodexEditorContextMenuGroup", "guidSHLMainMenu", "IDM_VS_CTXT_CODEWIN");
        RequireGroupParent(vsct, "CodexDebugMenuGroup", "guidSHLMainMenu", "IDM_VS_MENU_DEBUG");

        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexViewMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexProjectContextMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexSolutionContextMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexEditorContextMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidSHLMainMenu", "IDG_VS_WNDO_OTRWNDWS1");
        RequireCommandPlacement(vsct, "DebugWithCodexCommandId", "guidCommandSet", "CodexDebugMenuGroup");
        RequireCommandPlacement(vsct, "CreatePlanCommandId", "guidCommandSet", "CodexProjectContextMenuGroup");
        RequireCommandPlacement(vsct, "CreatePlanCommandId", "guidCommandSet", "CodexSolutionContextMenuGroup");

        RequireIdSymbol(vsct, "CodexViewMenuGroup");
        RequireIdSymbol(vsct, "CodexProjectContextMenuGroup");
        RequireIdSymbol(vsct, "CodexSolutionContextMenuGroup");
        RequireIdSymbol(vsct, "AskCodexCommandId");
        RequireIdSymbol(vsct, "ExplainSelectionCommandId");
        RequireIdSymbol(vsct, "FixSelectionCommandId");
        RequireIdSymbol(vsct, "ReviewSelectionCommandId");
        RequireIdSymbol(vsct, "OptimizeSelectionCommandId");
        RequireIdSymbol(vsct, "GenerateDocsCommandId");
        RequireIdSymbol(vsct, "ConfigureMemoryCommandId");
        RequireIdSymbol(vsct, "CodexDebugMenuGroup");
    }

    [Test]
    public void Codex_defaults_include_failover_budget_analytics_and_ReactiveMemory_hooks()
    {
        var models = ReadText("src/VSCodexExtension/Models/CodexModels.cs");
        var promptBuilder = ReadText("src/VSCodexExtension/Services/PromptBuilder.cs");
        var mcpConfig = ReadText("src/VSCodexExtension/Services/McpConfigService.cs");
        var view = ReadText("src/VSCodexExtension/Views/CodexToolWindowControl.xaml");
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
    public void Package_auto_loads_and_opens_Codex_tool_window_once_on_first_launch()
    {
        var packageSource = ReadText("src/VSCodexExtension/VSCodexExtensionPackage.cs");

        RequireContains(packageSource, "ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string", "Package must auto-load before a solution is open.");
        RequireContains(packageSource, "ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string", "Package must auto-load when a solution exists.");
        RequireContains(packageSource, "ShowToolWindowOnFirstLaunchAsync", "Package initialization must call a first-launch tool-window opener.");
        RequireContains(packageSource, "FirstLaunchToolWindowOpened", "First-launch state must be persisted so the window is not forced open every launch.");
        RequireContains(packageSource, "ShellSettingsManager", "First-launch state must use the Visual Studio settings store.");
        RequireContains(packageSource, "ShowToolWindowAsync(typeof(CodexToolWindowPane)", "First launch must show the Codex tool window pane.");
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
