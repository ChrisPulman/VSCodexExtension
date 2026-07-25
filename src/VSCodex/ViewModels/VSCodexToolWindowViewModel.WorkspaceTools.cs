// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI;

namespace VSCodex.ViewModels;

/// <summary>Coordinates workspace, MCP, skill, voice, and prerequisite features.</summary>
public sealed partial class VSCodexToolWindowViewModel
{
    /// <summary>Refreshes workspace Identity For Startup.</summary>
    private void RefreshWorkspaceIdentityForStartup()
    {
        try
        {
            _workspace.RefreshWorkspaceIdentity();
            RaiseWorkspaceDisplayProperties();
            string currentIdentity = _workspace.CurrentWorkspaceIdentity.Id;
            if (!string.IsNullOrWhiteSpace(currentIdentity) && !string.Equals(_lastWorkspaceSettingsId, currentIdentity, StringComparison.OrdinalIgnoreCase))
            {
                _lastWorkspaceSettingsId = currentIdentity;
                ApplySettingsFromStore(_settingsStore.LoadForWorkspace(_workspace.CurrentWorkspaceIdentity));
            }

            _lastWorkspaceIdentityId = currentIdentity;
            if (!string.IsNullOrWhiteSpace(_workspace.CurrentWorkspaceRoot))
            {
                _memoryStore.LoadWorkspace(_workspace.CurrentWorkspaceRoot);
            }

            Status = (string.IsNullOrWhiteSpace(_workspace.CurrentWorkspaceRoot) ? "VSCodex ready; open a solution or repository folder" : ($"VSCodex ready for {_workspace.CurrentWorkspaceRoot}"));
        }
        catch (Exception ex)
        {
            Status = $"VSCodex startup context deferred: {ex.Message}";
        }
    }

    /// <summary>Performs the raise Workspace Display Properties operation.</summary>
    private void RaiseWorkspaceDisplayProperties()
    {
        this.RaisePropertyChanged();
    }

    /// <summary>Performs the scan Project Memory operation.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ScanProjectMemoryAsync()
    {
        WorkspaceIdentity identity = _workspace.CurrentWorkspaceIdentity;
        if (identity is null || string.IsNullOrWhiteSpace(identity.RootPath))
        {
            Status = "Open a solution or repository folder before scanning project memory";
            return;
        }

        Status = "Scanning project memory with ReactiveMemory ProjectMiner...";
        ReactiveMemoryCallResult scan = await Task.Run(
            () => _reactiveMemory.ScanWorkspaceAsync(identity))
            .ConfigureAwait(continueOnCapturedContext: false);
        await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
        Status = scan.Message;
        if (scan.Success)
        {
            return;
        }

        _ = AddMessage(CodexMessageRole.System, scan.Message);
    }

    /// <summary>Ensures workspace Ready For Run.</summary>
    /// <returns><see langword="true"/> when ensure Workspace Ready For Run succeeds; otherwise, <see langword="false"/>.</returns>
    private bool EnsureWorkspaceReadyForRun()
    {
        string root = _workspace.CurrentWorkspaceRoot;
        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root) && !root.StartsWith(LocalPaths.ExtensionInstallRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string message = "VSCodex cannot run yet because Visual Studio has not provided a solution " +
            "or repository folder project root. Wait for the project to finish loading, open a " +
            "solution or repository folder, or use @ references after a project is available. " +
            "The installed VSIX folder will not be used as the execution root.";
        _ = AddMessage(CodexMessageRole.System, message);
        Status = "VSCodex waiting for Visual Studio project context";
        return false;
    }

    /// <summary>Selects mcp Server.</summary>
    /// <param name="server">The server.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task SelectMcpServerAsync(McpServerDefinition server)
    {
        if (server is null)
        {
            return;
        }

        await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
        SelectedMcpServer = server;
        Status = $"Discovering MCP tools for {server.Name}...";
        IReadOnlyList<McpToolDefinition> tools = await Task.Run(
            () => _mcpTools.DiscoverToolsAsync(server))
            .ConfigureAwait(continueOnCapturedContext: false);
        await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
        Replace(McpToolSuggestions, tools);
        Replace(McpToolInputFields, []);
        SelectedMcpTool = null;
        Status = ((tools.Count == 0) ? "No MCP tools discovered" : $"Discovered {tools.Count} MCP tool(s) for {server.Name}");
    }

    /// <summary>Selects mcp Tool.</summary>
    /// <param name="tool">The tool.</param>
    private void SelectMcpTool(McpToolDefinition tool)
    {
        if (tool is null)
        {
            return;
        }

        SelectedMcpTool = tool;
        Replace(McpToolInputFields, tool.InputFields.Select(CloneField));
        McpInputPrompt = ((tool.InputFields.Count == 0) ? "No input required." : "Provide values for the fields below. Optional fields show 'option' after the field name.");
        Status = $"Selected MCP tool {tool.DisplayName}";
    }

    /// <summary>Performs the insert Mcp Tool Invocation operation.</summary>
    private void InsertMcpToolInvocation()
    {
        if (SelectedMcpServer is null || SelectedMcpTool is null)
        {
            ShowMcpServerList();
            return;
        }

        SelectedMcpTool.InputFields.Clear();
        SelectedMcpTool.InputFields.AddRange(McpToolInputFields.Select(CloneField));
        string invocation = _mcpTools.BuildInvocationPrompt(SelectedMcpServer, SelectedMcpTool);
        Prompt = (string.IsNullOrWhiteSpace(Prompt) ? invocation : (Prompt.TrimEnd() + Environment.NewLine + invocation));
        Status = "Inserted MCP tool invocation into prompt";
    }

    /// <summary>Adds mcp Server.</summary>
    /// <param name="transportType">The transport Type.</param>
    private void AddMcpServer(string transportType)
    {
        if (!CanEditSettings)
        {
            Status = VSCodexSettingsAreLockedWhileATaskIsRunnText;
            return;
        }

        McpServerDefinition server = _mcpConfig.CreateTemplate(transportType);
        McpServers.Add(server);
        SelectedMcpServer = server;
        Status = "Added MCP server draft. Fill in the details, then save MCP servers.";
    }

    /// <summary>Removes mcp Server.</summary>
    /// <param name="server">The server.</param>
    private void RemoveMcpServer(McpServerDefinition server)
    {
        if (server is null || !CanEditSettings)
        {
            return;
        }

        _ = McpServers.Remove(server);
        if (SelectedMcpServer == server)
        {
            SelectedMcpServer = McpServers.FirstOrDefault();
        }

        Status = "Removed MCP server draft. Save MCP servers to update Codex config.";
    }

    /// <summary>Saves mcp Servers.</summary>
    private void SaveMcpServers()
    {
        if (!CanEditSettings)
        {
            Status = VSCodexSettingsAreLockedWhileATaskIsRunnText;
            return;
        }

        _mcpConfig.Save(McpServers.ToList());
        Status = $"Saved MCP servers to {LocalPaths.UserCodexConfig}";
    }

    /// <summary>Creates skill.</summary>
    private void CreateSkill()
    {
        if (!CanEditSettings)
        {
            Status = VSCodexSettingsAreLockedWhileATaskIsRunnText;
            return;
        }

        try
        {
            _ = Directory.CreateDirectory(LocalPaths.UserSkillsRoot);
            string skillPath = _skillIndex.CreateSkill(LocalPaths.UserSkillsRoot, NewSkillName, NewSkillDescription);
            NewSkillName = string.Empty;
            NewSkillDescription = string.Empty;
            Refresh();
            Status = $"Created skill {skillPath}";
            OpenPath(skillPath);
        }
        catch (Exception ex)
        {
            Status = $"Create skill failed: {ex.Message}";
        }
    }

    /// <summary>Saves skill Selection.</summary>
    private void SaveSkillSelection()
    {
        if (!CanEditSettings)
        {
            Status = VSCodexSettingsAreLockedWhileATaskIsRunnText;
            return;
        }

        ExtensionSettings settings = _settingsStore.Current;
        settings.EnabledSkillPaths = (from x in Skills
                                      where x.IsEnabled
                                      select x.MarkdownPath into x
                                      where !string.IsNullOrWhiteSpace(x)
                                      select x).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        SaveSettingsForCurrentWorkspace(settings);
        Status = $"Saved {settings.EnabledSkillPaths.Count} enabled skill(s)";
        UpdateAnalytics(Prompt);
    }

    /// <summary>Adds skill Root.</summary>
    private void AddSkillRoot()
    {
        if (!CanEditSettings)
        {
            Status = VSCodexSettingsAreLockedWhileATaskIsRunnText;
            return;
        }

        string path = SkillRootPathInput.Trim();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            Status = "Enter an existing folder to add a skill root";
            return;
        }

        ExtensionSettings settings = _settingsStore.Current;
        if (!settings.SkillRoots.Any((x) => string.Equals(x, path, StringComparison.OrdinalIgnoreCase)))
        {
            settings.SkillRoots.Add(path);
            SaveSettingsForCurrentWorkspace(settings);
        }

        SkillRootPathInput = string.Empty;
        Refresh();
        Status = $"Added skill root {path}";
    }

    /// <summary>Opens skills Folder.</summary>
    private void OpenSkillsFolder()
    {
        _ = Directory.CreateDirectory(LocalPaths.UserSkillsRoot);
        OpenPath(LocalPaths.UserSkillsRoot);
    }

    /// <summary>Opens codex Config.</summary>
    private void OpenCodexConfig()
    {
        string directory = Path.GetDirectoryName(LocalPaths.UserCodexConfig);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        if (!File.Exists(LocalPaths.UserCodexConfig))
        {
            File.WriteAllText(LocalPaths.UserCodexConfig, string.Empty);
        }

        OpenPath(LocalPaths.UserCodexConfig);
    }

    /// <summary>Handles the prompt Changed event.</summary>
    /// <param name="prompt">The prompt.</param>
    private void OnPromptChanged(string prompt)
    {
        int revision = Interlocked.Increment(ref _promptChangeRevision);
        TaskObserver.FireAndForget(_joinableTaskFactory.RunAsync(async () =>
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
            if (revision != Volatile.Read(ref _promptChangeRevision) || !string.Equals(prompt, Prompt, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                UpdateReferenceSuggestions(prompt);
                UpdatePromptSuggestions(prompt);
                UpdateAnalytics(prompt);
                if (IsMcpDiscoveryPrompt(prompt))
                {
                    ShowMcpServerList();
                }
            }
            catch (Exception ex)
            {
                Status = $"Prompt context update failed: {ex.Message}";
            }
        }).Task);
    }

    /// <summary>Performs the show Mcp Server List operation.</summary>
    private void ShowMcpServerList()
    {
        Replace(McpToolSuggestions, []);
        Replace(McpToolInputFields, []);
        McpInputPrompt = "Select an MCP server to list tools. Then select a tool and provide required input fields; optional fields show 'option'.";
        Status = ((McpServers.Count == 0) ? "No MCP servers are configured in .codex/config.toml" : "Select an MCP server from the MCP tab");
    }

    /// <summary>Attempts to handle Local Slash Command.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when try Handle Local Slash Command succeeds; otherwise, <see langword="false"/>.</returns>
    private bool TryHandleLocalSlashCommand(string value)
    {
        string command = (value ?? string.Empty).Trim();
        if (command.Length == 0 || command[0] != '/')
        {
            return false;
        }

        string commandName = (command.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty).ToLowerInvariant();
        switch (commandName)
        {
            case "/threads" or "/history":
                {
                    ShowHistory();
                    Prompt = string.Empty;
                    return true;
                }

            case "/models" or "/settings":
                {
                    Status = "Open Tools > Options > VSCodex to change settings";
                    Prompt = string.Empty;
                    return true;
                }

            case "/refresh":
                {
                    Refresh();
                    Prompt = string.Empty;
                    return true;
                }

            default:
                return TryHandleToolPanelSlashCommand(commandName);
        }
    }

    /// <summary>Attempts to handle a tool Panel Slash Command.</summary>
    /// <param name="commandName">The command Name.</param>
    /// <returns><see langword="true"/> when a tool panel command was handled; otherwise, <see langword="false"/>.</returns>
    private bool TryHandleToolPanelSlashCommand(string commandName)
    {
        (int TabIndex, string Title)? panel = commandName switch
        {
            "/setup" or "/prerequisites" => (1, "VSCodex prerequisites"),
            "/context" or "/files" or "/selection" => (Numeric2, "VSCodex context"),
            "/skills" => (Numeric3, "VSCodex skills"),
            "/tools" or "/mcp" => (Numeric4, "VSCodex MCP tools"),
            "/memory" => (Numeric5, "VSCodex memory"),
            "/agents" => (Numeric6, "VSCodex agents"),
            "/attachments" => (Numeric7, "VSCodex attachments"),
            _ => null
        };
        if (!panel.HasValue)
        {
            return false;
        }

        if (panel.Value.TabIndex == Numeric4)
        {
            ShowMcpServerList();
        }

        ShowToolPanel(panel.Value.TabIndex, panel.Value.Title);
        Prompt = string.Empty;
        return true;
    }

    /// <summary>Performs the show Tool Panel operation.</summary>
    /// <param name="tabIndex">The tab Index.</param>
    /// <param name="status">The status.</param>
    private void ShowToolPanel(int tabIndex, string status)
    {
        IsToolPanelOpen = true;
        SelectedToolTabIndex = tabIndex;
        Status = status;
    }

    /// <summary>Adds memory.</summary>
    /// <param name="scope">The scope.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task AddMemoryAsync(string scope)
    {
        string text = Prompt;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ReactiveMemoryCallResult memory = await _reactiveMemory.AddMemoryAsync(text, scope, _workspace.CurrentWorkspaceIdentity).ConfigureAwait(continueOnCapturedContext: false);
        await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
        if (memory.Success)
        {
            _memoryStore.Add(text, scope);
            Status = memory.Message;
        }
        else
        {
            Status = $"ReactiveMemory did not save memory: {memory.Message}";
        }
    }

    /// <summary>Adds image Attachment.</summary>
    private void AddImageAttachment()
    {
        OpenFileDialog dialog = new OpenFileDialog
        {
            Title = "Attach files for VSCodex",
            Filter = "Supported files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp;*.pdf;*.doc;" +
                "*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt;*.md;*.cs;*.xaml;*.json;*.xml|" +
                "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|Documents|*.pdf;*.doc;" +
                "*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt;*.md|All files|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        AttachFiles(dialog.FileNames);
    }

    /// <summary>Performs the append Voice Transcript operation.</summary>
    /// <param name="text">The text.</param>
    private void AppendVoiceTranscript(string text)
    {
        string transcript = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return;
        }

        bool shouldSubmit = TryExtractVoiceSubmit(ref transcript);
        if (!string.IsNullOrWhiteSpace(transcript))
        {
            Prompt = (string.IsNullOrWhiteSpace(Prompt) ? transcript : ($"{Prompt.TrimEnd()} {transcript}"));
            VoiceTranscriptRevision++;
            Status = (shouldSubmit ? "Voice command added; sending VSCodex request" : "Voice transcript added");
        }
        else if (shouldSubmit)
        {
            Status = (string.IsNullOrWhiteSpace(Prompt) ? "Voice command heard; dictate a prompt before saying send" : "Voice command sending VSCodex request");
        }

        if (!shouldSubmit || string.IsNullOrWhiteSpace(Prompt))
        {
            return;
        }

        _voiceInput.Stop();
        this.RaisePropertyChanged();
        TaskObserver.FireAndForget(_joinableTaskFactory.RunAsync(
            async () => await SubmitPromptAsync().ConfigureAwait(continueOnCapturedContext: true)).Task);
    }

    /// <summary>Updates voice Input Status.</summary>
    /// <param name="status">The status.</param>
    private void UpdateVoiceInputStatus(string status)
    {
        VoiceInputStatus = status;
        this.RaisePropertyChanged();
        this.RaisePropertyChanged();
    }

    /// <summary>Performs the schedule Startup Checks In Background operation.</summary>
    private void ScheduleStartupChecksInBackground()
    {
        TaskObserver.FireAndForget(_joinableTaskFactory.RunAsync(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Numeric4), _lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
                await _joinableTaskFactory.SwitchToMainThreadAsync(_lifetime.Token);
                Status = "Loading VSCodex tools and checking setup in the background...";
                string workspaceRoot = _workspace.CurrentWorkspaceRoot;
                List<string> skillRoots = _settingsStore.Current.SkillRoots.Concat([Path.Combine(workspaceRoot ?? string.Empty, ".codex", "skills")]).ToList();
                await Task.Run(
                    () =>
                {
                    _mcpConfig.Refresh();
                    _skillIndex.Refresh(skillRoots);
                },
                    _lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
                await CheckPrerequisitesAsync(showSystemMessage: false).ConfigureAwait(continueOnCapturedContext: true);
                await Task.Delay(TimeSpan.FromSeconds(Numeric6), _lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
                await RefreshRateLimitsAsync().ConfigureAwait(continueOnCapturedContext: true);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception ex2)
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
                Status = $"VSCodex background startup checks failed: {ex2.Message}";
            }
        }).Task);
    }

    /// <summary>Checks prerequisites.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task CheckPrerequisitesAsync()
    {
        return CheckPrerequisitesAsync(showSystemMessage: true);
    }

    /// <summary>Checks prerequisites.</summary>
    /// <param name="showSystemMessage">The show System Message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task CheckPrerequisitesAsync(bool showSystemMessage)
    {
        await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
        Status = CheckingVSCodexPrerequisitesText;
        CodexSetupSummary = CheckingVSCodexPrerequisitesText;
        CodexEnvironmentReport report = await _environment.CheckAsync(_settingsStore.Current).ConfigureAwait(continueOnCapturedContext: false);
        await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
        ApplyEnvironmentReport(report, showSystemMessage && !report.CanRunSdkBridge);
    }

    /// <summary>Refreshes rate Limits In Background.</summary>
    private void RefreshRateLimitsInBackground()
    {
        TaskObserver.FireAndForget(_joinableTaskFactory.RunAsync(
            async () => await RefreshRateLimitsAsync().ConfigureAwait(continueOnCapturedContext: true)).Task);
    }

    /// <summary>Refreshes rate Limits.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task RefreshRateLimitsAsync()
    {
        CodexEnvironmentReport? report = _lastEnvironmentReport;
        if (report?.CanRunSdkBridge == false)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
            SetRateLimitsUnavailable("Codex SDK unavailable");
            return;
        }

        await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
        SetRateLimitRows("Fetching Codex telemetry", 0, string.Empty);
        RateLimitUpdatedAt = "Checking Codex rate-limit telemetry";
        try
        {
            JObject? rateLimits = await _codex.GetRateLimitsAsync().ConfigureAwait(continueOnCapturedContext: false);
            await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
            if (rateLimits is null)
            {
                SetRateLimitsUnavailable("Codex telemetry unavailable");
                return;
            }

            UpdateRateLimitsFromJson(rateLimits.ToString());
        }
        catch (Exception ex)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
            SetRateLimitsUnavailable("Codex telemetry unavailable");
            RateLimitUpdatedAt = $"Codex rate-limit check failed: {ex.Message}";
        }
    }

    /// <summary>Sets rate Limits Unavailable.</summary>
    /// <param name="text">The text.</param>
    private void SetRateLimitsUnavailable(string text)
    {
        SetRateLimitRows(text, 0, string.Empty);
    }

    /// <summary>Sets rate Limit Rows.</summary>
    /// <param name="remaining">The remaining.</param>
    /// <param name="usagePercent">The usage Percent.</param>
    /// <param name="resetText">The reset Text.</param>
    private void SetRateLimitRows(string remaining, int usagePercent, string resetText)
    {
        foreach (RateLimitWindowStatus rateLimit in RateLimits)
        {
            rateLimit.Remaining = remaining;
            rateLimit.UsagePercent = usagePercent;
            rateLimit.ResetText = resetText;
        }
    }

    /// <summary>Ensures codex Sdk Ready For Run.</summary>
    /// <returns>A task whose result contains the operation result.</returns>
    private async Task<bool> EnsureCodexSdkReadyForRunAsync()
    {
        CodexEnvironmentReport? report = _lastEnvironmentReport;
        if (report?.CanRunSdkBridge != true)
        {
            report = await _environment.CheckAsync(_settingsStore.Current).ConfigureAwait(continueOnCapturedContext: false);
            await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
            ApplyEnvironmentReport(report, !report.CanRunSdkBridge);
        }

        if (report.CanRunSdkBridge)
        {
            return true;
        }

        _ = AddMessage(CodexMessageRole.System, CodexSetupInstructions);
        Status = "VSCodex setup required. Open Tools > Options > VSCodex to adjust runtime paths.";
        return false;
    }

    /// <summary>Applies environment Report.</summary>
    /// <param name="report">The report.</param>
    /// <param name="showSystemMessage">The show System Message.</param>
    private void ApplyEnvironmentReport(CodexEnvironmentReport report, bool showSystemMessage)
    {
        _lastEnvironmentReport = report;
        Replace(Prerequisites, report.Items);
        CodexSetupSummary = report.Summary;
        CodexSetupInstructions = report.Instructions;
        Status = (report.CanRunSdkBridge ? "VSCodex prerequisites ready" : "VSCodex setup required");
        if (!report.CanRunSdkBridge)
        {
            IsToolPanelOpen = true;
            SelectedToolTabIndex = 1;
        }

        if (!showSystemMessage)
        {
            return;
        }

        _ = AddMessage(CodexMessageRole.System, report.Summary + Environment.NewLine + Environment.NewLine + report.Instructions);
    }

    /// <summary>Copies prerequisite Command To Clipboard.</summary>
    /// <param name="prerequisite">The prerequisite.</param>
    private void CopyPrerequisiteCommandToClipboard(PrerequisiteStatus? prerequisite)
    {
        string? command = prerequisite?.ActionCommand;
        if (prerequisite is null || command is null || command.Trim().Length == 0)
        {
            Status = "No prerequisite command to copy";
            return;
        }

        try
        {
            Clipboard.SetText(command);
            Status = $"Copied {prerequisite.Name} prerequisite command";
        }
        catch (Exception ex)
        {
            Status = $"Could not copy prerequisite command: {ex.Message}";
        }
    }

    /// <summary>Starts prerequisite Update.</summary>
    /// <param name="prerequisite">The prerequisite.</param>
    private void StartPrerequisiteUpdate(PrerequisiteStatus? prerequisite)
    {
        string? command = prerequisite?.ActionCommand;
        if (prerequisite is null || command is null || command.Trim().Length == 0)
        {
            Status = "No prerequisite update command available";
            return;
        }

        try
        {
            _ = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/d /s /k {QuoteForCmd($"{command} && echo. && echo VSCodex prerequisite command finished. Restart Visual Studio if PATH changed, then run Check again.")}",
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                UseShellExecute = true
            });
            Status = $"Started prerequisite update for {prerequisite.Name}";
        }
        catch (Exception ex)
        {
            Status = $"Could not start prerequisite update: {ex.Message}";
        }
    }

    /// <summary>Starts new Thread.</summary>
    private void StartNewThread()
    {
        SaveCurrentSessionIfNeeded();
        _session = _sessionStore.Create();
        Prompt = string.Empty;
        ThreadId = null;
        Messages.Clear();
        RunActivityRoots.Clear();
        _activeRunActivity = null;
        _activeProgressNode = null;
        _pendingUserActivityPromptToSuppress = string.Empty;
        _pausedCheckpoint = null;
        _pauseRequested = false;
        _stopRequested = false;
        IsPaused = false;
        Attachments.Clear();
        OrchestrationSections.Clear();
        Status = "New VSCodex thread";
        RefreshHistory();
        UpdateAnalytics(Prompt);
    }

    /// <summary>Applies current Workspace To Session.</summary>
    private void ApplyCurrentWorkspaceToSession()
    {
        WorkspaceIdentity identity = _workspace.CurrentWorkspaceIdentity;
        if (identity is null || string.IsNullOrWhiteSpace(identity.Id))
        {
            return;
        }

        _session.WorkspaceIdentityId = identity.Id;
        _session.WorkspaceName = (string.IsNullOrWhiteSpace(identity.Name) ? _workspace.CurrentWorkspaceName : identity.Name);
        _session.WorkspaceRoot = identity.RootPath;
        _session.WorkspaceSolutionPath = identity.SolutionPath;
    }
}
