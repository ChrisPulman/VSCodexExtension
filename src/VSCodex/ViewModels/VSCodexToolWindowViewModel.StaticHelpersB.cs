// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace VSCodex.ViewModels;

/// <summary>Provides stateless helpers for events, telemetry, and formatting.</summary>
public sealed partial class VSCodexToolWindowViewModel
{
    /// <summary>Performs the activity Kind For Role operation.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The activity Kind For Role result.</returns>
    private static RunActivityKind ActivityKindForRole(CodexMessageRole role) => role switch
    {
        CodexMessageRole.Assistant => RunActivityKind.Assistant,
        CodexMessageRole.Memory or CodexMessageRole.Mcp => RunActivityKind.Mcp,
        CodexMessageRole.Skill => RunActivityKind.Skill,
        CodexMessageRole.System or CodexMessageRole.Error => RunActivityKind.System,
        _ => RunActivityKind.Agent,
    };

    /// <summary>Performs the activity Title For Role operation.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The activity Title For Role result.</returns>
    private static string ActivityTitleForRole(CodexMessageRole role)
    {
        return role switch
        {
            CodexMessageRole.Assistant => "Final assistant response",
            CodexMessageRole.Mcp => "MCP event",
            CodexMessageRole.Memory => "ReactiveMemory",
            CodexMessageRole.Skill => "Skill event",
            CodexMessageRole.Error => "Error",
            CodexMessageRole.System => "System",
            _ => "Agent event",
        };
    }

    /// <summary>Performs the collect Changed Files For Workspace operation.</summary>
    /// <param name="workspaceRoot">The workspace Root.</param>
    /// <returns>The collect Changed Files For Workspace result.</returns>
    private static IReadOnlyList<ChangedFileActivity> CollectChangedFilesForWorkspace(string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) || !Directory.Exists(workspaceRoot))
        {
            return [];
        }

        try
        {
            using Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-C {QuoteForCmd(workspaceRoot)} status --porcelain=v1 --untracked-files=all",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process is null)
            {
                return [];
            }

            string output = process.StandardOutput.ReadToEnd();
            return !process.WaitForExit(Numeric5000) || process.ExitCode != 0 ? [] : (from line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                                                                                      select ParseChangedFileLine(workspaceRoot, line) into file
                                                                                      where file is not null
                                                                                      select file).Cast<ChangedFileActivity>().ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Parses changed File Line.</summary>
    /// <param name="root">The root.</param>
    /// <param name="line">The line.</param>
    /// <returns>The parse Changed File Line result.</returns>
    private static ChangedFileActivity? ParseChangedFileLine(string root, string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length < Numeric4)
        {
            return null;
        }

        string status = line.Substring(0, Numeric2).Trim();
        string path = line.Substring(Numeric3).Trim();
        int renameIndex = path.IndexOf(" -> ", StringComparison.Ordinal);
        if (renameIndex >= 0)
        {
            path = path.Substring(renameIndex + Numeric4).Trim();
        }

        path = path.Trim('"');
        string fullPath = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
        return new ChangedFileActivity
        {
            RelativePath = path,
            FullPath = fullPath,
            Status = (string.IsNullOrWhiteSpace(status) ? "modified" : status),
            IsDeleted = (status.IndexOf("D", StringComparison.OrdinalIgnoreCase) >= 0 || !File.Exists(fullPath))
        };
    }

    /// <summary>Performs the clone Settings operation.</summary>
    /// <param name="source">The source.</param>
    /// <returns>The clone Settings result.</returns>
    private static ExtensionSettings CloneSettings(ExtensionSettings source)
    {
        return new ExtensionSettings
        {
            CodexCliPath = source.CodexCliPath,
            NodePath = source.NodePath,
            BridgeScriptPath = source.BridgeScriptPath,
            DefaultModel = source.DefaultModel,
            DefaultFailoverModel = source.DefaultFailoverModel,
            DefaultReasoningEffort = source.DefaultReasoningEffort,
            DefaultVerbosity = source.DefaultVerbosity,
            DefaultServiceTier = source.DefaultServiceTier,
            DefaultProfile = source.DefaultProfile,
            DefaultApprovalPolicy = source.DefaultApprovalPolicy,
            DefaultSandboxMode = source.DefaultSandboxMode,
            CustomModels = CloneList(source.CustomModels),
            CustomReasoningEfforts = CloneList(source.CustomReasoningEfforts),
            CustomVerbosityOptions = CloneList(source.CustomVerbosityOptions),
            SkillRoots = CloneList(source.SkillRoots),
            EnabledSkillPaths = CloneList(source.EnabledSkillPaths),
            DefaultUseMultiAgentOrchestration = source.DefaultUseMultiAgentOrchestration,
            DefaultMaxAgentConcurrency = source.DefaultMaxAgentConcurrency,
            DefaultAgentStrategy = source.DefaultAgentStrategy,
            DefaultOrchestrationModel = source.DefaultOrchestrationModel,
            DefaultBudgetDrivenModelSelection = source.DefaultBudgetDrivenModelSelection,
            DefaultBudgetModel = source.DefaultBudgetModel,
            DefaultFollowUpBehavior = source.DefaultFollowUpBehavior,
            DefaultInputAreaHeight = source.DefaultInputAreaHeight,
            AgentRoles = CloneAgentRoles(source.AgentRoles)
        };
    }

    /// <summary>Clones a sequence into a mutable list.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The optional source items.</param>
    /// <returns>The cloned list.</returns>
    private static List<T> CloneList<T>(IEnumerable<T>? items)
    {
        return items is null ? new List<T>() : items.ToList();
    }

    /// <summary>Resolves a blank value to a supplied default.</summary>
    /// <param name="value">The candidate value.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <returns>The candidate value, or the default when blank.</returns>
    private static string DefaultIfBlank(string? value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value ?? defaultValue;
    }

    /// <summary>Clones the supplied agent roles.</summary>
    /// <param name="agentRoles">The agent roles to clone.</param>
    /// <returns>The cloned agent roles.</returns>
    private static List<AgentRoleDefinition> CloneAgentRoles(List<AgentRoleDefinition>? agentRoles)
    {
        return DistinctAgentRoles(agentRoles ?? [])
            .Select(CloneAgentRole)
            .ToList();
    }

    /// <summary>Performs the clone Workspace Identity operation.</summary>
    /// <param name="source">The source.</param>
    /// <returns>The clone Workspace Identity result.</returns>
    private static WorkspaceIdentity? CloneWorkspaceIdentity(WorkspaceIdentity? source)
    {
        return source is null ? null : new WorkspaceIdentity
        {
            Id = source.Id,
            Name = source.Name,
            RootPath = source.RootPath,
            SolutionPath = source.SolutionPath,
            SolutionRelativePath = source.SolutionRelativePath,
            RepositoryRemote = source.RepositoryRemote,
            MemoryRoot = source.MemoryRoot
        };
    }

    /// <summary>Ensures model Option.</summary>
    /// <param name="models">The models.</param>
    /// <param name="model">The model.</param>
    private static void EnsureModelOption(List<string> models, string model)
    {
        if (string.IsNullOrWhiteSpace(model) || models.Any((x) => x.Equals(model, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        models.Add(model.Trim());
    }

    /// <summary>Performs the last Reference Token operation.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <returns>The last Reference Token result.</returns>
    private static string? LastReferenceToken(string prompt)
    {
        return (prompt ?? string.Empty)
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault((x) => x.StartsWith("@", StringComparison.Ordinal) || x.StartsWith("#", StringComparison.Ordinal));
    }

    /// <summary>Performs the last Prompt Token operation.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <returns>The last Prompt Token result.</returns>
    private static string? LastPromptToken(string prompt)
    {
        int start = LastPromptTokenStart(prompt);
        return start >= 0 ? prompt.Remove(0, start) : null;
    }

    /// <summary>Performs the last Prompt Token Start operation.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <returns>The last Prompt Token Start result.</returns>
    private static int LastPromptTokenStart(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return -1;
        }

        if (char.IsWhiteSpace(prompt.Last()))
        {
            return -1;
        }

        string trimmedEnd = prompt.TrimEnd();
        int index = trimmedEnd.Length - 1;
        while (index >= 0 && !char.IsWhiteSpace(trimmedEnd[index]))
        {
            index--;
        }

        int start = index + 1;
        if (start >= trimmedEnd.Length)
        {
            return -1;
        }

        char marker = trimmedEnd[start];
        return marker is not '@' and not '#' && marker != '/' ? -1 : start;
    }

    /// <summary>Determines whether is Mcp Discovery Prompt.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <returns><see langword="true"/> when is Mcp Discovery Prompt succeeds; otherwise, <see langword="false"/>.</returns>
    private static bool IsMcpDiscoveryPrompt(string prompt)
    {
        return (prompt ?? string.Empty).Trim().StartsWith("/MCP", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Performs the clamp Input Height operation.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The clamp Input Height result.</returns>
    private static double ClampInputHeight(double value)
    {
        return Math.Max(Numeric32Point0, Math.Min(Numeric600Point0, (value <= 0.0) ? Numeric180 : value));
    }

    /// <summary>Formats elapsed.</summary>
    /// <param name="elapsed">The elapsed.</param>
    /// <returns>The format Elapsed result.</returns>
    private static string FormatElapsed(TimeSpan elapsed)
    {
        return !(elapsed.TotalHours >= 1.0) ? elapsed.ToString("m\\:ss") : elapsed.ToString("h\\:mm\\:ss");
    }

    /// <summary>Builds default Rate Limits.</summary>
    /// <returns>The build Default Rate Limits result.</returns>
    private static IReadOnlyList<RateLimitWindowStatus> BuildDefaultRateLimits()
    {
        return [
            new RateLimitWindowStatus
            {
                Label = "5h",
                Remaining = "Waiting for Codex telemetry",
                UsagePercent = 0,
                ResetText = string.Empty
            },
            new RateLimitWindowStatus
            {
                Label = WeeklyText,
                Remaining = "Waiting for Codex telemetry",
                UsagePercent = 0,
                ResetText = string.Empty
            }
        ];
    }

    /// <summary>Finds rate Limit Token.</summary>
    /// <param name="root">The root.</param>
    /// <returns>The find Rate Limit Token result.</returns>
    private static JToken? FindRateLimitToken(JToken root)
    {
        JToken?[] array = [
            root,
            root.SelectToken("rateLimits", errorWhenNoMatch: false),
            root.SelectToken("rate_limits", errorWhenNoMatch: false),
            root.SelectToken("result.rateLimits", errorWhenNoMatch: false),
            root.SelectToken("result.rate_limits", errorWhenNoMatch: false),
            root.SelectToken("result.rateLimits.rate_limits", errorWhenNoMatch: false),
            root.SelectToken("result.rateLimits.rateLimits", errorWhenNoMatch: false),
            root.SelectToken("rateLimitsByLimitId.codex", errorWhenNoMatch: false),
            root.SelectToken("result.rateLimitsByLimitId.codex", errorWhenNoMatch: false),
            root.SelectToken("result.result.rateLimits", errorWhenNoMatch: false),
            root.SelectToken("result.result.rate_limits", errorWhenNoMatch: false),
            root.SelectToken("result.result.rateLimitsByLimitId.codex", errorWhenNoMatch: false),
            root.SelectToken("usage.rateLimits", errorWhenNoMatch: false),
            root.SelectToken("usage.rate_limits", errorWhenNoMatch: false),
            root.SelectToken("result.usage.rateLimits", errorWhenNoMatch: false),
            root.SelectToken("result.usage.rate_limits", errorWhenNoMatch: false)
        ];
        for (int i = 0; i < array.Length; i++)
        {
            JToken? unwrapped = UnwrapRateLimitToken(array[i]);
            if (LooksLikeRateLimitToken(unwrapped))
            {
                return unwrapped;
            }
        }

        foreach (JToken item in root.SelectTokens("$..rate_limits").Concat(root.SelectTokens("$..rateLimits")))
        {
            JToken? unwrapped2 = UnwrapRateLimitToken(item);
            if (LooksLikeRateLimitToken(unwrapped2))
            {
                return unwrapped2;
            }
        }

        return null;
    }

    /// <summary>Performs the unwrap Rate Limit Token operation.</summary>
    /// <param name="token">The token.</param>
    /// <returns>The unwrap Rate Limit Token result.</returns>
    private static JToken? UnwrapRateLimitToken(JToken? token)
    {
        return token is null ? null : SelectFirstToken(token, "rate_limits", "rateLimits") ?? token;
    }

    /// <summary>Performs the looks Like Rate Limit Token operation.</summary>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> when looks Like Rate Limit Token succeeds; otherwise, <see langword="false"/>.</returns>
    private static bool LooksLikeRateLimitToken(JToken? token)
    {
        return SelectFirstToken(token, "primary", "secondary", "fiveHour", "five_hour", "weekly", "week", "requests.primary", "requests.secondary") is not null;
    }

    /// <summary>Selects first Token.</summary>
    /// <param name="token">The token.</param>
    /// <param name="paths">The paths.</param>
    /// <returns>The select First Token result.</returns>
    private static JToken? SelectFirstToken(JToken? token, params string[] paths)
    {
        if (token is null)
        {
            return null;
        }

        foreach (string path in paths)
        {
            JToken? value = token.SelectToken(path, errorWhenNoMatch: false);
            if (value is not null && value.Type != JTokenType.Null)
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>Formats rate Limit Reset.</summary>
    /// <param name="label">The label.</param>
    /// <param name="resetAt">The reset At.</param>
    /// <returns>The format Rate Limit Reset result.</returns>
    private static string FormatRateLimitReset(string label, DateTimeOffset resetAt)
    {
        DateTimeOffset local = resetAt.ToLocalTime();
        return !string.Equals(label, WeeklyText, StringComparison.OrdinalIgnoreCase) ? local.ToString("HH:mm") : local.ToString("d MMM");
    }

    /// <summary>Performs the clamp Percent operation.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The clamp Percent result.</returns>
    private static int ClampPercent(int value)
    {
        return Math.Max(0, Math.Min(Numeric100, value));
    }

    /// <summary>Performs the token String operation.</summary>
    /// <param name="token">The token.</param>
    /// <param name="names">The names.</param>
    /// <returns>The token String result.</returns>
    private static string? TokenString(JToken token, params string[] names)
    {
        foreach (string name in names)
        {
            JToken? value = token[name];
            if (value is not null && value.Type != JTokenType.Null)
            {
                return value.Value<string>() ?? value.ToString();
            }
        }

        return null;
    }

    /// <summary>Performs the token Int operation.</summary>
    /// <param name="token">The token.</param>
    /// <param name="names">The names.</param>
    /// <returns>The token Int result.</returns>
    private static int? TokenInt(JToken token, params string[] names)
    {
        return !int.TryParse(TokenString(token, names), out var parsed) ? null : parsed;
    }

    /// <summary>Performs the token Long operation.</summary>
    /// <param name="token">The token.</param>
    /// <param name="names">The names.</param>
    /// <returns>The token Long result.</returns>
    private static long? TokenLong(JToken token, params string[] names)
    {
        return !long.TryParse(TokenString(token, names), out var parsed) ? null : parsed;
    }

    /// <summary>Performs the clone Field operation.</summary>
    /// <param name="field">The field.</param>
    /// <returns>The clone Field result.</returns>
    private static McpToolInputField CloneField(McpToolInputField field)
    {
        return new McpToolInputField
        {
            Name = field.Name,
            Type = field.Type,
            Description = field.Description,
            IsRequired = field.IsRequired,
            Value = field.Value
        };
    }

    /// <summary>Performs the infer Attachment Kind operation.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The infer Attachment Kind result.</returns>
    private static string InferAttachmentKind(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (new string[Numeric6] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" }.Contains(ext))
        {
            return "image";
        }

        return new string[9] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".md" }.Contains(ext) ? "document" : "file";
    }

    /// <summary>Formats prompt File Reference.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The format Prompt File Reference result.</returns>
    private static string FormatPromptFileReference(string path)
    {
        string value = path ?? string.Empty;
        if (value.IndexOfAny([' ', '\t', '\r', '\n']) >= 0)
        {
            value = $"\"{value.Replace("\"", "\\\"")}\"";
        }

        return $"@{value}";
    }
}
