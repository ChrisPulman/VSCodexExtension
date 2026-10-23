// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Newtonsoft.Json.Linq;

namespace VSCodex.ViewModels;

/// <summary>Composes protocol-backed history with local display preferences.</summary>
public sealed partial class VSCodexToolWindowViewModel
{
    /// <summary>Names the protocol thread response member.</summary>
    private const string ThreadPropertyName = "thread";

    /// <summary>Names protocol identity members.</summary>
    private const string IdentifierPropertyName = "id";

    /// <summary>Names protocol text members and content kinds.</summary>
    private const string TextPropertyName = "text";

    /// <summary>Stores whether archived history is visible.</summary>
    private bool _includeArchivedSessions;

    /// <summary>Gets the command that opens the native active-document diff.</summary>
    public ReactiveCommand<Unit, Unit> ReviewActiveFileCommand { get; private set; }

    /// <summary>Gets the command that imports public app-server thread metadata.</summary>
    public ReactiveCommand<Unit, Unit> RefreshThreadsCommand { get; private set; }

    /// <summary>Gets the command that forks a saved conversation.</summary>
    public ReactiveCommand<SessionHistoryItem, Unit> ForkSessionCommand { get; private set; }

    /// <summary>Gets the command that toggles a conversation's archive state.</summary>
    public ReactiveCommand<SessionHistoryItem, Unit> ArchiveSessionCommand { get; private set; }

    /// <summary>Gets the command that toggles local pinning.</summary>
    public ReactiveCommand<SessionHistoryItem, Unit> PinSessionCommand { get; private set; }

    /// <summary>Gets or sets whether archived conversations are included.</summary>
    public bool IncludeArchivedSessions
    {
        get => _includeArchivedSessions;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _includeArchivedSessions, value);
            RefreshHistory();
        }
    }

    /// <summary>Gets the optional public app-server capability.</summary>
    private IThreadCatalogClient ThreadCatalog => _codex as IThreadCatalogClient
        ?? throw new NotSupportedException("Thread management requires the Codex app-server transport.");

    /// <summary>Opens the native editor comparison without changing repository contents.</summary>
    /// <returns>A task that completes when the operation finishes.</returns>
    private async Task ReviewActiveFileAsync()
    {
        try
        {
            await new NativeDiffReviewService().ReviewActiveFileAsync(_workspace.CurrentWorkspaceRoot);
            Status = "Opened native HEAD comparison";
        }
        catch (Exception exception)
        {
            Status = $"Could not open file review: {exception.Message}";
        }
    }

    /// <summary>Refreshes the catalog without overwriting local transcript or preferences.</summary>
    /// <returns>A task that completes when the operation finishes.</returns>
    private async Task RefreshThreadsAsync()
    {
        try
        {
            SaveCurrentSessionIfNeeded();
            await ImportThreadCatalogAsync(false);
            if (IncludeArchivedSessions)
            {
                await ImportThreadCatalogAsync(true);
            }

            RefreshHistory();
            Status = "Refreshed Codex thread history";
        }
        catch (Exception exception)
        {
            Status = $"Could not refresh Codex threads: {exception.Message}";
        }
    }

    /// <summary>Imports a catalog page group for the current workspace.</summary>
    /// <param name="archived">The archive state.</param>
    /// <returns>A task that completes when the operation finishes.</returns>
    private async Task ImportThreadCatalogAsync(bool archived)
    {
        string workspaceRoot = _workspace.CurrentWorkspaceRoot;
        JObject catalog = await ThreadCatalog.ListThreadsAsync(archived);
        if (!string.Equals(workspaceRoot, _workspace.CurrentWorkspaceRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The workspace changed while loading history; refresh again.");
        }

        List<CodexSessionDocument> existing = _sessionStore.LoadRecent(int.MaxValue).ToList();
        foreach (JObject thread in catalog["data"]?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
        {
            ImportThreadMetadata(thread, archived, workspaceRoot, existing);
        }
    }

    /// <summary>Imports metadata for a thread belonging to the captured workspace.</summary>
    /// <param name="thread">The protocol metadata.</param>
    /// <param name="archived">The archive state.</param>
    /// <param name="workspaceRoot">The captured workspace root.</param>
    /// <param name="existing">The existing local documents.</param>
    private void ImportThreadMetadata(JObject thread, bool archived, string workspaceRoot, List<CodexSessionDocument> existing)
    {
        string id = thread.Value<string>(IdentifierPropertyName) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id) || !SameWorkspacePath(thread.Value<string>("cwd"), workspaceRoot))
        {
            return;
        }

        CodexSessionDocument session = existing.FirstOrDefault(x => x.ThreadId == id) ?? _sessionStore.Create();
        session.ThreadId = id;
        session.IsArchived = archived;
        if (string.IsNullOrWhiteSpace(session.Title))
        {
            session.Title = thread.Value<string>("name") ?? thread.Value<string>("preview") ?? id;
        }

        ApplyWorkspaceToSession(session, _workspace.CurrentWorkspaceIdentity);
        if (thread["updatedAt"]?.Type == JTokenType.Integer)
        {
            session.Updated = DateTimeOffset.FromUnixTimeSeconds(thread.Value<long>("updatedAt"));
        }

        _sessionStore.Save(session, preserveUpdated: true);
    }

    /// <summary>Loads public persisted turns before opening a catalog-only entry.</summary>
    /// <param name="item">The history entry.</param>
    /// <returns>A task that completes when the operation finishes.</returns>
    private async Task LoadHistoryItemAsync(SessionHistoryItem item)
    {
        try
        {
            CodexSessionDocument? session = _sessionStore.Load(item.Id);
            if (session?.Messages.Count == 0 && !string.IsNullOrWhiteSpace(session.ThreadId))
            {
                JObject response = await ThreadCatalog.ReadThreadAsync(session.ThreadId!);
                ImportThreadMessages(session, response[ThreadPropertyName] as JObject);
                _sessionStore.Save(session, preserveUpdated: true);
            }

            LoadHistoryItem(item);
        }
        catch (Exception exception)
        {
            Status = $"Could not load Codex thread: {exception.Message}";
        }
    }

    /// <summary>Forks server-side history without reusing the source thread identifier.</summary>
    /// <param name="item">The source history entry.</param>
    /// <returns>A task that completes when the operation finishes.</returns>
    private async Task ForkSessionAsync(SessionHistoryItem item)
    {
        try
        {
            if (item is null || string.IsNullOrWhiteSpace(item.ThreadId))
            {
                Status = "This history entry has no Codex thread to fork";
                return;
            }

            SaveCurrentSessionIfNeeded();
            WorkspaceIdentity workspace = _workspace.CurrentWorkspaceIdentity;
            JObject result = await ThreadCatalog.ForkThreadAsync(item.ThreadId!);
            JObject thread = result[ThreadPropertyName] as JObject ?? throw new InvalidOperationException("Codex did not return the forked thread.");
            CodexSessionDocument fork = _sessionStore.Create();
            fork.ThreadId = thread.Value<string>(IdentifierPropertyName) ?? throw new InvalidOperationException("Codex returned no fork identifier.");
            fork.Title = $"{item.Title} (fork)";
            ApplyWorkspaceToSession(fork, workspace);
            CodexSessionDocument? source = _sessionStore.Load(item.Id);
            if (source?.Messages.Count > 0)
            {
                fork.Messages.AddRange(source.Messages.Where(message => !message.IsTransient).Select(CloneMessage));
            }
            else
            {
                ImportThreadMessages(fork, thread);
            }

            _sessionStore.Save(fork);
            RefreshHistory();
            if (!string.Equals(workspace.Id, _workspace.CurrentWorkspaceIdentity.Id, StringComparison.OrdinalIgnoreCase))
            {
                Status = "Fork saved in the source workspace history";
                return;
            }

            await LoadHistoryItemAsync(BuildHistoryItem(fork));
        }
        catch (Exception exception)
        {
            Status = $"Could not fork Codex thread: {exception.Message}";
        }
    }

    /// <summary>Updates the server first so failed mutations leave local metadata intact.</summary>
    /// <param name="item">The history entry.</param>
    /// <returns>A task that completes when the operation finishes.</returns>
    private async Task ArchiveSessionAsync(SessionHistoryItem item)
    {
        try
        {
            CodexSessionDocument? session = _sessionStore.Load(item.Id);
            if (session is null)
            {
                return;
            }

            bool archived = !session.IsArchived;
            if (!string.IsNullOrWhiteSpace(session.ThreadId))
            {
                _ = await ThreadCatalog.SetThreadArchivedAsync(session.ThreadId!, archived);
            }

            session.IsArchived = archived;
            _sessionStore.Save(session);
            if (_session.Id == session.Id)
            {
                _session.IsArchived = archived;
            }

            RefreshHistory();
            Status = archived ? "Archived conversation" : "Restored conversation";
        }
        catch (Exception exception)
        {
            Status = $"Could not update archive state: {exception.Message}";
        }
    }

    /// <summary>Persists pinning as an extension-local display preference.</summary>
    /// <param name="item">The history entry.</param>
    private void PinSession(SessionHistoryItem item)
    {
        CodexSessionDocument? session = _sessionStore.Load(item.Id);
        if (session is null)
        {
            return;
        }

        session.IsPinned = !session.IsPinned;
        _sessionStore.Save(session);
        if (_session.Id == session.Id)
        {
            _session.IsPinned = session.IsPinned;
        }

        RefreshHistory();
        Status = session.IsPinned ? "Pinned conversation locally" : "Unpinned conversation";
    }
}
