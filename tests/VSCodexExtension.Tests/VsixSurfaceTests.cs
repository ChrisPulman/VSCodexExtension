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
        var slnx = ReadText("VSCodexExtension.slnx");
        RequireContains(slnx, "tests/VSCodexExtension.Tests/VSCodexExtension.Tests.csproj", "Solution must include the TUnit/MTP regression test project.");

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

        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexViewMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexProjectContextMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexSolutionContextMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidCommandSet", "CodexEditorContextMenuGroup");
        RequireCommandPlacement(vsct, "OpenToolWindowCommandId", "guidSHLMainMenu", "IDG_VS_WNDO_OTRWNDWS1");

        RequireIdSymbol(vsct, "CodexViewMenuGroup");
        RequireIdSymbol(vsct, "CodexProjectContextMenuGroup");
        RequireIdSymbol(vsct, "CodexSolutionContextMenuGroup");
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

    private static string ReadText(string relativePath) => File.ReadAllText(PathFor(relativePath));

    private static string PathFor(string relativePath) => Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string LocateRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "VSCodexExtension.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }
}
