using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace VSCodex.Services;

internal static class VisualStudioMenuIntegrationService
{
    private const int MsoControlPopup = 10;
    private static bool _installed;

    private static readonly MenuCommandSpec OpenToolWindow = new("View.VSCodex", "VSCodex Tool Window");

    private static readonly MenuCommandSpec[] CoreActions =
    {
        OpenToolWindow,
        new("VSCodex.Settings", "VSCodex Settings"),
        new("VSCodex.AddToChat", "Add to VSCodex Chat"),
        new("VSCodex.Explain", "Explain"),
        new("VSCodex.FixWithVSCodex", "Fix with VSCodex"),
        new("VSCodex.FixSelection", "Fix Selection"),
        new("VSCodex.Review", "Review Selection"),
        new("VSCodex.Optimize", "Optimize Selection"),
        new("VSCodex.GenerateComments", "Generate Comments"),
        new("VSCodex.GenerateTests", "Generate Tests"),
        new("VSCodex.Debug", "Debug With VSCodex"),
        new("VSCodex.FixActiveException", "Fix Active Exception"),
        new("VSCodex.FixTestFailure", "Fix Test Failure"),
        new("VSCodex.CreatePlan", "Create Agent Plan"),
        new("VSCodex.ReactiveMemorySetup", "VSCodex ReactiveMemory Setup")
    };

    private static readonly MenuCommandSpec[] CodeActions =
    {
        OpenToolWindow,
        new("VSCodex.AddToChat", "Add to VSCodex Chat"),
        new("VSCodex.Explain", "Explain"),
        new("VSCodex.FixWithVSCodex", "Fix with VSCodex"),
        new("VSCodex.FixSelection", "Fix Selection"),
        new("VSCodex.Review", "Review Selection"),
        new("VSCodex.Optimize", "Optimize Selection"),
        new("VSCodex.GenerateComments", "Generate Comments"),
        new("VSCodex.GenerateTests", "Generate Tests"),
        new("VSCodex.Debug", "Debug With VSCodex"),
        new("VSCodex.CreatePlan", "Create Agent Plan")
    };

    private static readonly string[] SolutionExplorerContextBars =
    {
        "Solution",
        "Project",
        "Item",
        "Folder",
        "Solution Folder",
        "Cross Project Multi Project",
        "Cross Project Multi Item"
    };

    public static async Task InitializeAsync(AsyncPackage package)
    {
        if (_installed)
        {
            return;
        }

        await package.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

        try
        {
            var dte = await package.GetServiceAsync(typeof(DTE)).ConfigureAwait(true) as DTE;
            if (dte?.CommandBars == null)
            {
                ActivityLog.TryLogWarning(nameof(VisualStudioMenuIntegrationService), "DTE command bars were unavailable; VSCodex runtime menu bridge was skipped.");
                return;
            }

            InstallMainMenus(dte);
            InstallContextMenus(dte);
            _installed = true;
        }
        catch (Exception ex)
        {
            ActivityLog.TryLogError(nameof(VisualStudioMenuIntegrationService), ex.ToString());
        }
    }

    private static void InstallMainMenus(DTE dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var menuBar = TryGetCommandBar(dte.CommandBars, "MenuBar");
        if (menuBar == null)
        {
            return;
        }

        var topLevel = EnsurePopup(menuBar, "VSCodex");
        if (topLevel != null)
        {
            AddCommands(dte, GetPopupCommandBar(topLevel), CoreActions);
        }

        var viewMenu = FindControlByCaption(menuBar, "View");
        if (viewMenu != null)
        {
            var viewCommandBar = GetPopupCommandBar(viewMenu);
            var viewPopup = viewCommandBar == null ? null : EnsurePopup(viewCommandBar, "VSCodex");
            if (viewPopup != null)
            {
                AddCommands(dte, GetPopupCommandBar(viewPopup), CoreActions);
            }
        }

        var toolsMenu = FindControlByCaption(menuBar, "Tools");
        if (toolsMenu != null)
        {
            var toolsCommandBar = GetPopupCommandBar(toolsMenu);
            var toolsPopup = toolsCommandBar == null ? null : EnsurePopup(toolsCommandBar, "VSCodex");
            if (toolsPopup != null)
            {
                AddCommands(dte, GetPopupCommandBar(toolsPopup), CoreActions);
            }
        }

        var debugMenu = FindControlByCaption(menuBar, "Debug");
        if (debugMenu != null)
        {
            AddCommands(
                dte,
                GetPopupCommandBar(debugMenu),
                new[]
                {
                    new MenuCommandSpec("VSCodex.Debug", "Debug With VSCodex"),
                    new MenuCommandSpec("VSCodex.FixActiveException", "Fix Active Exception"),
                    new MenuCommandSpec("VSCodex.FixWithVSCodex", "Fix with VSCodex")
                });
        }

        var testMenu = FindControlByCaption(menuBar, "Test");
        if (testMenu != null)
        {
            AddCommands(
                dte,
                GetPopupCommandBar(testMenu),
                new[]
                {
                    new MenuCommandSpec("VSCodex.GenerateTests", "Generate Tests"),
                    new MenuCommandSpec("VSCodex.FixTestFailure", "Fix Test Failure")
                });
        }
    }

    private static void InstallContextMenus(DTE dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        foreach (var barName in new[] { "Code Window", "Text Editor" })
        {
            AddActionsPopup(dte, barName, CodeActions);
        }

        foreach (var barName in SolutionExplorerContextBars)
        {
            AddActionsPopup(dte, barName, CodeActions);
        }

        foreach (var barName in new[] { "Error List", "Task List" })
        {
            var commandBar = TryGetCommandBar(dte.CommandBars, barName);
            if (commandBar != null)
            {
                AddCommands(
                    dte,
                    commandBar,
                    new[]
                    {
                        OpenToolWindow,
                        new MenuCommandSpec("VSCodex.FixWithVSCodex", "Fix with VSCodex"),
                        new MenuCommandSpec("VSCodex.Debug", "Debug With VSCodex")
                    });
            }
        }
    }

    private static void AddActionsPopup(DTE dte, string commandBarName, IEnumerable<MenuCommandSpec> commands)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var commandBar = TryGetCommandBar(dte.CommandBars, commandBarName);
        if (commandBar == null)
        {
            return;
        }

        var popup = EnsurePopup(commandBar, "VSCodex Actions");
        if (popup != null)
        {
            AddCommands(dte, GetPopupCommandBar(popup), commands);
        }
    }

    private static void AddCommands(DTE dte, object? commandBar, IEnumerable<MenuCommandSpec> commands)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (commandBar == null)
        {
            return;
        }

        var position = 1;
        foreach (var command in commands)
        {
            DeleteControl(commandBar, command.Caption);
            var dteCommand = TryGetCommand(dte, command.CanonicalName);
            if (dteCommand == null)
            {
                ActivityLog.TryLogWarning(nameof(VisualStudioMenuIntegrationService), $"VSCodex command '{command.CanonicalName}' was not available for runtime menu placement.");
                continue;
            }

            try
            {
                var addedControl = dteCommand.AddControl(commandBar, position++);
                if (addedControl != null)
                {
                    SetVisible(addedControl);
                }
            }
            catch (Exception ex)
            {
                ActivityLog.TryLogWarning(nameof(VisualStudioMenuIntegrationService), $"Could not add '{command.Caption}' to a Visual Studio menu: {ex.Message}");
            }
        }
    }

    private static Command? TryGetCommand(DTE dte, string canonicalName)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            return dte.Commands.Item(canonicalName, -1);
        }
        catch
        {
            return null;
        }
    }

    private static object? TryGetCommandBar(object commandBars, string name)
    {
        try
        {
            return commandBars.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, commandBars, new object[] { name });
        }
        catch
        {
            return null;
        }
    }

    private static object? EnsurePopup(object commandBar, string caption)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var existing = FindControlByCaption(commandBar, caption);
        if (existing != null)
        {
            return existing;
        }

        try
        {
            var controls = GetControls(commandBar);
            if (controls == null)
            {
                return null;
            }

            var popup = controls.GetType().InvokeMember(
                "Add",
                BindingFlags.InvokeMethod,
                null,
                controls,
                new object[] { MsoControlPopup, Type.Missing, Type.Missing, Type.Missing, true });

            SetProperty(popup, "Caption", caption);
            SetProperty(popup, "TooltipText", caption);
            SetVisible(popup);
            return popup;
        }
        catch (Exception ex)
        {
            ActivityLog.TryLogWarning(nameof(VisualStudioMenuIntegrationService), $"Could not create '{caption}' menu popup: {ex.Message}");
            return null;
        }
    }

    private static object? FindControlByCaption(object commandBar, string caption)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var controls = GetControls(commandBar);
        if (controls == null)
        {
            return null;
        }

        foreach (var control in EnumerateControls(controls))
        {
            var controlCaption = GetStringProperty(control, "Caption");
            if (string.Equals(NormalizeCaption(controlCaption), NormalizeCaption(caption), StringComparison.OrdinalIgnoreCase))
            {
                return control;
            }
        }

        return null;
    }

    private static void DeleteControl(object commandBar, string caption)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var controls = GetControls(commandBar);
        if (controls == null)
        {
            return;
        }

        foreach (var control in EnumerateControls(controls))
        {
            var controlCaption = GetStringProperty(control, "Caption");
            if (!string.Equals(NormalizeCaption(controlCaption), NormalizeCaption(caption), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                control.GetType().InvokeMember("Delete", BindingFlags.InvokeMethod, null, control, new object[] { false });
            }
            catch
            {
                try
                {
                    control.GetType().InvokeMember("Delete", BindingFlags.InvokeMethod, null, control, Array.Empty<object>());
                }
                catch
                {
                }
            }
        }
    }

    private static object? GetPopupCommandBar(object popup)
    {
        return GetProperty(popup, "CommandBar");
    }

    private static object? GetControls(object commandBar)
    {
        return GetProperty(commandBar, "Controls");
    }

    private static object? GetProperty(object target, string propertyName)
    {
        try
        {
            return target.GetType().InvokeMember(propertyName, BindingFlags.GetProperty, null, target, Array.Empty<object>());
        }
        catch
        {
            return null;
        }
    }

    private static string GetStringProperty(object target, string propertyName)
    {
        return GetProperty(target, propertyName) as string ?? string.Empty;
    }

    private static void SetProperty(object target, string propertyName, object value)
    {
        target.GetType().InvokeMember(propertyName, BindingFlags.SetProperty, null, target, new[] { value });
    }

    private static void SetVisible(object target)
    {
        try
        {
            SetProperty(target, "Visible", true);
        }
        catch
        {
        }
    }

    private static IEnumerable<object> EnumerateControls(object controls)
    {
        var count = 0;
        try
        {
            count = Convert.ToInt32(GetProperty(controls, "Count"));
        }
        catch
        {
        }

        for (var i = 1; i <= count; i++)
        {
            object? control = null;
            try
            {
                control = controls.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, controls, new object[] { i });
            }
            catch
            {
            }

            if (control != null)
            {
                yield return control;
            }
        }
    }

    private static string NormalizeCaption(string caption)
        => caption.Replace("&", string.Empty).Trim();

    private sealed class MenuCommandSpec
    {
        public MenuCommandSpec(string canonicalName, string caption)
        {
            CanonicalName = canonicalName;
            Caption = caption;
        }

        public string CanonicalName { get; }
        public string Caption { get; }
    }
}
