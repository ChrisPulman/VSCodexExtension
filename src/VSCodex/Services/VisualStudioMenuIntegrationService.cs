using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using VSCodex.Commands;

namespace VSCodex.Services;

internal static class VisualStudioMenuIntegrationService
{
    private const int MsoControlPopup = 10;
    private static bool _installed;

    private static readonly MenuCommandSpec OpenToolWindow = new("View.VSCodex", "VSCodex", CodexCommandIds.OpenToolWindowCommandId);

    private static readonly MenuCommandSpec[] CoreActions =
    {
        OpenToolWindow,
        new("VSCodex.Settings", "VSCodex Settings", CodexCommandIds.OpenOptionsCommandId),
        new("VSCodex.AddToChat", "Add to VSCodex Chat", CodexCommandIds.AskCodexCommandId),
        new("VSCodex.Explain", "Explain", CodexCommandIds.ExplainSelectionCommandId),
        new("VSCodex.FixWithVSCodex", "Fix with VSCodex", CodexCommandIds.FixActiveErrorCommandId),
        new("VSCodex.FixSelection", "Fix Selection", CodexCommandIds.FixSelectionCommandId),
        new("VSCodex.Review", "Review Selection", CodexCommandIds.ReviewSelectionCommandId),
        new("VSCodex.Optimize", "Optimize Selection", CodexCommandIds.OptimizeSelectionCommandId),
        new("VSCodex.GenerateComments", "Generate Comments", CodexCommandIds.GenerateDocsCommandId),
        new("VSCodex.GenerateTests", "Generate Tests", CodexCommandIds.CreateTestFromSelectionCommandId),
        new("VSCodex.Debug", "Debug With VSCodex", CodexCommandIds.DebugWithCodexCommandId),
        new("VSCodex.FixActiveException", "Fix Active Exception", CodexCommandIds.FixActiveExceptionCommandId),
        new("VSCodex.FixTestFailure", "Fix Test Failure", CodexCommandIds.FixTestFailureCommandId),
        new("VSCodex.CreatePlan", "Create Agent Plan", CodexCommandIds.CreatePlanCommandId),
        new("VSCodex.ReactiveMemorySetup", "VSCodex ReactiveMemory Setup", CodexCommandIds.ConfigureMemoryCommandId)
    };

    public static async Task InitializeAsync(AsyncPackage package)
    {
        if (_installed)
        {
            return;
        }

        for (var attempt = 1; attempt <= 10 && !_installed; attempt++)
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            try
            {
                var dte = await package.GetServiceAsync(typeof(DTE)).ConfigureAwait(true) as DTE;
                if (dte?.CommandBars == null)
                {
                    ActivityLog.TryLogWarning(nameof(VisualStudioMenuIntegrationService), "DTE command bars were unavailable; VSCodex runtime menu bridge will retry.");
                }
                else if (InstallMainMenus(dte))
                {
                    InstallContextMenus(dte);
                    _installed = true;
                    return;
                }
                else
                {
                    // Keep retrying quietly; the static VSCT menu entries remain usable even
                    // when DTE does not expose the command names early in startup.
                }

                await Task.Delay(TimeSpan.FromSeconds(2), package.DisposalToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (package.DisposalToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                ActivityLog.TryLogError(nameof(VisualStudioMenuIntegrationService), ex.ToString());
                await Task.Delay(TimeSpan.FromSeconds(2), package.DisposalToken).ConfigureAwait(false);
            }
        }

        if (!_installed)
        {
            ActivityLog.TryLogWarning(nameof(VisualStudioMenuIntegrationService), "VSCodex static VSCT menu entries are available, but runtime DTE menu repair did not resolve the commands during startup.");
        }
    }

    private static bool InstallMainMenus(DTE dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var menuBar = TryGetCommandBar(dte.CommandBars, "MenuBar");
        if (menuBar == null)
        {
            return false;
        }

        var topLevel = EnsurePopup(menuBar, "VSCodex");
        if (topLevel != null)
        {
            AddCommands(dte, GetPopupCommandBar(topLevel), CoreActions);
        }

        var viewMenu = FindControlByCaption(menuBar, "View");
        var openCommandVisible = false;
        if (viewMenu != null)
        {
            var viewCommandBar = GetPopupCommandBar(viewMenu);

            if (viewCommandBar != null)
            {
                openCommandVisible = AddCommands(dte, viewCommandBar, new[] { OpenToolWindow }) > 0;
            }

            var viewPopup = viewCommandBar == null ? null : EnsurePopup(viewCommandBar, "VSCodex Actions");
            if (viewPopup != null)
            {
                AddCommands(dte, GetPopupCommandBar(viewPopup), CoreActions);
            }

            var otherWindowsPopup = viewCommandBar == null ? null : FindControlByCaption(viewCommandBar, "Other Windows");
            if (otherWindowsPopup != null)
            {
                SetVisible(otherWindowsPopup);
                AddCommands(dte, GetPopupCommandBar(otherWindowsPopup), new[] { OpenToolWindow });
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
                    new MenuCommandSpec("VSCodex.Debug", "Debug With VSCodex", CodexCommandIds.DebugWithCodexCommandId),
                    new MenuCommandSpec("VSCodex.FixActiveException", "Fix Active Exception", CodexCommandIds.FixActiveExceptionCommandId),
                    new MenuCommandSpec("VSCodex.FixWithVSCodex", "Fix with VSCodex", CodexCommandIds.FixActiveErrorCommandId)
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
                    new MenuCommandSpec("VSCodex.GenerateTests", "Generate Tests", CodexCommandIds.CreateTestFromSelectionCommandId),
                    new MenuCommandSpec("VSCodex.FixTestFailure", "Fix Test Failure", CodexCommandIds.FixTestFailureCommandId)
                });
        }

        return openCommandVisible;
    }

    private static void InstallContextMenus(DTE dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        // Context menus are declared through VSCT only. Runtime DTE insertion can
        // create duplicate editor entries after the command table is refreshed.
    }

    private static int AddCommands(DTE dte, object? commandBar, IEnumerable<MenuCommandSpec> commands)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (commandBar == null)
        {
            return 0;
        }

        var position = 1;
        var placed = 0;
        foreach (var command in commands)
        {
            var dteCommand = TryGetCommand(dte, command);
            if (dteCommand == null)
            {
                ActivityLog.TryLogWarning(nameof(VisualStudioMenuIntegrationService), $"VSCodex command '{command.CanonicalName}' was not available for runtime menu placement.");
                continue;
            }

            try
            {
                var existing = FindControlByCaption(commandBar, command.Caption);
                if (existing != null)
                {
                    SetVisible(existing);
                    placed++;
                    continue;
                }

                var addedControl = dteCommand.AddControl(commandBar, position++);
                if (addedControl != null)
                {
                    SetVisible(addedControl);
                    placed++;
                }
            }
            catch (Exception ex)
            {
                ActivityLog.TryLogWarning(nameof(VisualStudioMenuIntegrationService), $"Could not add '{command.Caption}' to a Visual Studio menu: {ex.Message}");
            }
        }

        return placed;
    }

    private static Command? TryGetCommand(DTE dte, MenuCommandSpec command)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            return dte.Commands.Item(command.CanonicalName, -1);
        }
        catch
        {
        }

        try
        {
            return dte.Commands.Item(CodexCommandIds.CommandSetGuidString, command.CommandId);
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
            SetVisible(existing);
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
        public MenuCommandSpec(string canonicalName, string caption, int commandId)
        {
            CanonicalName = canonicalName;
            Caption = caption;
            CommandId = commandId;
        }

        public string CanonicalName { get; }
        public string Caption { get; }
        public int CommandId { get; }
    }
}
