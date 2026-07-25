// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace VSCodex.ViewModels;

/// <summary>Declares private helper types used by the VSCodex tool-window view model.</summary>
public sealed partial class VSCodexToolWindowViewModel
{
    /// <summary>Provides the changed File Activity implementation.</summary>
    private sealed class ChangedFileActivity
    {
        /// <summary>Gets or sets the relative Path.</summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>Gets or sets the full Path.</summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>Gets or sets the status.</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Gets or sets the is Deleted.</summary>
        public bool IsDeleted { get; set; }
    }

    /// <summary>Provides the composite Disposable Like implementation.</summary>
    /// <param name="items">The items to dispose.</param>
    private sealed class CompositeDisposableLike(params IDisposable[] items) : IDisposable
    {
        /// <summary>Stores the items.</summary>
        private readonly IDisposable[] _items = items;

        /// <summary>Performs the dispose operation.</summary>
        public void Dispose()
        {
            IDisposable[] items = _items;
            for (int i = 0; i < items.Length; i++)
            {
                items[i].Dispose();
            }
        }
    }

    /// <summary>Captures immutable state for one queued prompt execution.</summary>
    private sealed class QueuedPromptContext
    {
        /// <summary>Gets or sets the prompt.</summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>Gets or sets the run activity root.</summary>
        public RunActivityNode RunRoot { get; set; } = new();

        /// <summary>Gets or sets the workspace identity.</summary>
        public WorkspaceIdentity WorkspaceIdentity { get; set; } = new();

        /// <summary>Gets or sets the workspace root.</summary>
        public string WorkspaceRoot { get; set; } = string.Empty;

        /// <summary>Gets or sets the workspace name.</summary>
        public string WorkspaceName { get; set; } = string.Empty;

        /// <summary>Gets or sets the solution path.</summary>
        public string SolutionPath { get; set; } = string.Empty;

        /// <summary>Gets or sets the memory root.</summary>
        public string MemoryRoot { get; set; } = string.Empty;

        /// <summary>Gets or sets the thread identifier snapshot.</summary>
        public string? ThreadId { get; set; }

        /// <summary>Gets or sets the hash references.</summary>
        public IReadOnlyList<WorkspaceFileReference> HashReferences { get; set; } = [];

        /// <summary>Gets or sets the selected agents.</summary>
        public IReadOnlyList<AgentRoleDefinition> SelectedAgents { get; set; } = [];

        /// <summary>Gets or sets the enabled skills.</summary>
        public IReadOnlyList<SkillDefinition> Skills { get; set; } = [];

        /// <summary>Gets or sets the enabled MCP servers.</summary>
        public IReadOnlyList<McpServerDefinition> McpServers { get; set; } = [];

        /// <summary>Gets or sets the attachments.</summary>
        public IReadOnlyList<CodexAttachment> Attachments { get; set; } = [];

        /// <summary>Gets or sets the run options.</summary>
        public CodexRunOptions Options { get; set; } = new();
    }

    /// <summary>Composes the services required by the tool-window view model.</summary>
    /// <param name="values">The ordered package-composition dependencies.</param>
    private sealed class ViewModelDependencies(object[] values)
    {
        /// <summary>The Codex orchestrator dependency index.</summary>
        private const int CodexIndex = 8;

        /// <summary>The task orchestrator dependency index.</summary>
        private const int TaskOrchestratorIndex = 9;

        /// <summary>The model analytics dependency index.</summary>
        private const int ModelAnalyticsIndex = 11;

        /// <summary>The voice-input dependency index.</summary>
        private const int VoiceInputIndex = 13;

        /// <summary>The time-provider dependency index.</summary>
        private const int TimeProviderIndex = 14;

        /// <summary>The joinable-task-factory dependency index.</summary>
        private const int JoinableTaskFactoryIndex = 15;

        /// <summary>Gets the settings store.</summary>
        public ISettingsStore SettingsStore => Get<ISettingsStore>(0);

        /// <summary>Gets the memory store.</summary>
        public IMemoryStore MemoryStore => Get<IMemoryStore>(1);

        /// <summary>Gets the skill index.</summary>
        public ISkillIndexService SkillIndex => Get<ISkillIndexService>(Numeric2);

        /// <summary>Gets the MCP configuration.</summary>
        public IMcpConfigService McpConfig => Get<IMcpConfigService>(Numeric3);

        /// <summary>Gets the MCP tool catalog.</summary>
        public IMcpToolCatalogService McpTools => Get<IMcpToolCatalogService>(Numeric4);

        /// <summary>Gets the ReactiveMemory service.</summary>
        public IReactiveMemoryService ReactiveMemory => Get<IReactiveMemoryService>(Numeric5);

        /// <summary>Gets the workspace service.</summary>
        public IWorkspaceContextService Workspace => Get<IWorkspaceContextService>(Numeric6);

        /// <summary>Gets the session store.</summary>
        public ISessionStore SessionStore => Get<ISessionStore>(Numeric7);

        /// <summary>Gets the Codex orchestrator.</summary>
        public ICodexOrchestrator Codex => Get<ICodexOrchestrator>(CodexIndex);

        /// <summary>Gets the task orchestrator.</summary>
        public ITaskOrchestrationService TaskOrchestrator =>
            Get<ITaskOrchestrationService>(TaskOrchestratorIndex);

        /// <summary>Gets the assistant context.</summary>
        public ICodingAssistantContextService AssistantContext => Get<ICodingAssistantContextService>(Numeric10);

        /// <summary>Gets the model analytics service.</summary>
        public IModelAnalyticsService ModelAnalytics =>
            Get<IModelAnalyticsService>(ModelAnalyticsIndex);

        /// <summary>Gets the environment service.</summary>
        public ICodexEnvironmentService Environment => Get<ICodexEnvironmentService>(Numeric12);

        /// <summary>Gets the voice-input service.</summary>
        public IVoiceInputService VoiceInput => Get<IVoiceInputService>(VoiceInputIndex);

        /// <summary>Gets the time provider.</summary>
        public TimeProvider TimeProvider => Get<TimeProvider>(TimeProviderIndex);

        /// <summary>Gets the joinable task factory.</summary>
        public JoinableTaskFactory JoinableTaskFactory =>
            Get<JoinableTaskFactory>(JoinableTaskFactoryIndex);

        /// <summary>Gets one typed dependency from the legacy argument array.</summary>
        /// <typeparam name="T">The dependency type.</typeparam>
        /// <param name="index">The dependency index.</param>
        /// <returns>The typed dependency.</returns>
        private T Get<T>(int index)
            where T : class
        {
            if (values.Length != Numeric16)
            {
                throw new ArgumentException(
                    $"Expected {Numeric16} view-model dependencies but received {values.Length}.",
                    nameof(values));
            }

            return values[index] as T
                ?? throw new ArgumentException(
                    $"Dependency {index} must implement {typeof(T).FullName}.",
                    nameof(values));
        }
    }
}
