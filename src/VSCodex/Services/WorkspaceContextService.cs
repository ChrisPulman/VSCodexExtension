// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Provides the workspace Context Service implementation.</summary>
public sealed class WorkspaceContextService : IWorkspaceContextService
{
    /// <summary>Named number used by this type.</summary>
    private const int Numeric12 = 12;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric12000 = 12_000;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric2 = 2;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric2048 = 2048;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric3 = 3;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric4 = 4;

    /// <summary>Named string used by this type.</summary>
    private const string SelectionText = "selection";

    /// <summary>Defines the max Indexed Files.</summary>
    private const int MaxIndexedFiles = 5000;

    /// <summary>Matches hash references in a prompt.</summary>
    private static readonly Regex HashReferenceRegex = new("""(?<!#)#(?:"(?<quoted>[^"]+)"|(?<plain>[^\s,;\)\]\}]+))""");

    /// <summary>Matches at references in a prompt.</summary>
    private static readonly Regex AtReferenceRegex = new("""@(?:"(?<quoted>[^"]+)"|(?<plain>[^\s,;\)\]\}]+))""");

    /// <summary>Stores the service Provider.</summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>Stores the workspace Root.</summary>
    private readonly BehaviorSubject<string> _workspaceRoot = new(string.Empty);

    /// <summary>Stores the index Gate.</summary>
    private readonly object _indexGate = new();

    /// <summary>Stores the workspace File Index.</summary>
    private List<WorkspaceFileReference> _workspaceFileIndex = new();

    /// <summary>Stores the workspace Name.</summary>
    private string _workspaceName = string.Empty;

    /// <summary>Stores the solution Path.</summary>
    private string _solutionPath = string.Empty;

    /// <summary>Stores the workspace Memory Root.</summary>
    private string _workspaceMemoryRoot = string.Empty;

    /// <summary>Stores the workspace Identity.</summary>
    private WorkspaceIdentity _workspaceIdentity = new();

    /// <summary>Initializes a new instance of the <see cref="WorkspaceContextService"/> class.</summary>
    /// <param name="serviceProvider">The service Provider.</param>
    public WorkspaceContextService(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    /// <summary>Gets the workspace Root.</summary>
    public IObservable<string> WorkspaceRoot => _workspaceRoot.AsObservable();

    /// <summary>Gets the current Workspace Root.</summary>
    public string CurrentWorkspaceRoot => _workspaceRoot.Value;

    /// <summary>Gets the current Workspace Name.</summary>
    public string CurrentWorkspaceName => _workspaceName;

    /// <summary>Gets the current Solution Path.</summary>
    public string CurrentSolutionPath => _solutionPath;

    /// <summary>Gets the current Workspace Memory Root.</summary>
    public string CurrentWorkspaceMemoryRoot => _workspaceMemoryRoot;

    /// <summary>Gets the current Workspace Identity.</summary>
    public WorkspaceIdentity CurrentWorkspaceIdentity => _workspaceIdentity;

    /// <summary>Refreshes the operation.</summary>
    public void Refresh()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        RefreshCore(rebuildIndex: true);
    }

    /// <summary>Refreshes workspace Identity.</summary>
    public void RefreshWorkspaceIdentity()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        RefreshCore(rebuildIndex: false);
    }

    /// <summary>Performs the search Files operation.</summary>
    /// <param name="query">The query.</param>
    /// <param name="limit">The limit.</param>
    /// <returns>The search Files result.</returns>
    public IReadOnlyList<WorkspaceFileReference> SearchFiles(string query, int limit) => SearchFilesCore(query, limit);

    /// <summary>Performs the search Context References operation.</summary>
    /// <param name="query">The query.</param>
    /// <param name="limit">The limit.</param>
    /// <returns>The search Context References result.</returns>
    public IReadOnlyList<WorkspaceFileReference> SearchContextReferences(string query, int limit)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return SearchContextReferencesCore(query, limit);
    }

    /// <summary>Resolves mentions.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <param name="maxBytesPerFile">The max Bytes Per File.</param>
    /// <returns>The resolve Mentions result.</returns>
    public IReadOnlyList<WorkspaceFileReference> ResolveMentions(string prompt, int maxBytesPerFile) => ResolveMentionsCore(prompt, maxBytesPerFile);

    /// <summary>Resolves hash References.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <param name="maxBytesPerReference">The max Bytes Per Reference.</param>
    /// <returns>The resolve Hash References result.</returns>
    public IReadOnlyList<WorkspaceFileReference> ResolveHashReferences(string prompt, int maxBytesPerReference)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return ResolveHashReferencesCore(prompt, maxBytesPerReference);
    }

    /// <summary>Gets current Selection Reference.</summary>
    /// <param name="maxChars">The max Chars.</param>
    /// <returns>The get Current Selection Reference result.</returns>
    public WorkspaceFileReference? GetCurrentSelectionReference(int maxChars)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return GetCurrentSelectionReferenceCore(maxChars);
    }

    /// <summary>Refreshes core.</summary>
    /// <param name="rebuildIndex">The rebuild Index.</param>
    private void RefreshCore(bool rebuildIndex)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var dte = _serviceProvider.GetService(typeof(DTE)) as DTE;
        var solutionPath = GetSolutionPath(dte);
        var startDirectory = ResolveWorkspaceStartDirectory(solutionPath, GetOpenFolderDirectory(dte), GetActiveProjectDirectory(dte), GetActiveDocumentDirectory(dte));
        var root = ResolveWorkspaceRoot(startDirectory);
        var identity = BuildWorkspaceIdentity(root, solutionPath);
        _workspaceIdentity = identity;
        _solutionPath = solutionPath;
        _workspaceName = identity.Name;
        _workspaceMemoryRoot = identity.MemoryRoot;
        _workspaceRoot.OnNext(root);
        if (!rebuildIndex)
        {
            return;
        }

        RebuildWorkspaceFileIndex(root, dte);
    }

    /// <summary>Performs the search Files operation.</summary>
    /// <param name="query">The query.</param>
    /// <param name="limit">The limit.</param>
    /// <returns>The search Files result.</returns>
    private IReadOnlyList<WorkspaceFileReference> SearchFilesCore(string query, int limit)
    {
        if (limit <= 0)
        {
            return [];
        }

        var term = NormalizeReferenceToken(query, '@');
        var explicitMatches = SearchExplicitPath(term, limit);
        if (explicitMatches.Count > 0)
        {
            return explicitMatches;
        }

        var root = CurrentWorkspaceRoot;
        var files = SnapshotWorkspaceFileIndex(root);
        return files
            .Where(x => string.IsNullOrWhiteSpace(term)
                || x.RelativePath.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
                || x.Path.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(x => RankFileMatch(x, term))
            .ThenBy(x => x.RelativePath.Length)
            .Take(limit)
            .Select(x => WithPreview(x, Numeric2048, '@'))
            .ToList();
    }

    /// <summary>Performs the search Context References operation.</summary>
    /// <param name="query">The query.</param>
    /// <param name="limit">The limit.</param>
    /// <returns>The search Context References result.</returns>
    private IReadOnlyList<WorkspaceFileReference> SearchContextReferencesCore(string query, int limit)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (limit <= 0)
        {
            return [];
        }

        var term = NormalizeReferenceToken(query, '#');
        var results = new List<WorkspaceFileReference>();
        var selection = GetCurrentSelectionReference(Numeric12000);
        if (selection is not null && MatchesSelectionQuery(selection, term))
        {
            results.Add(selection);
        }

        var remaining = Math.Max(0, limit - results.Count);
        if (remaining > 0)
        {
            foreach (var file in SearchFiles(term, remaining))
            {
                results.Add(new WorkspaceFileReference
                {
                    Path = file.Path,
                    RelativePath = file.RelativePath,
                    Preview = file.Preview,
                    ReferenceKind = "file",
                    ReferenceKey = $"#{file.RelativePath}"
                });
            }
        }

        return results;
    }

    /// <summary>Resolves mentions.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <param name="maxBytesPerFile">The max Bytes Per File.</param>
    /// <returns>The resolve Mentions result.</returns>
    private IReadOnlyList<WorkspaceFileReference> ResolveMentionsCore(string prompt, int maxBytesPerFile)
    {
        return ExtractTokens(prompt, '@')
            .SelectMany(m => SearchFiles(m, 1))
            .Select(f => new WorkspaceFileReference
            {
                Path = f.Path,
                RelativePath = f.RelativePath,
                Preview = SafePreview(f.Path, maxBytesPerFile),
                ReferenceKind = "file",
                ReferenceKey = FormatReferenceKey('@', f.RelativePath)
            })
            .ToList();
    }

    /// <summary>Resolves hash References.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <param name="maxBytesPerReference">The max Bytes Per Reference.</param>
    /// <returns>The resolve Hash References result.</returns>
    private IReadOnlyList<WorkspaceFileReference> ResolveHashReferencesCore(string prompt, int maxBytesPerReference)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var resolved = new List<WorkspaceFileReference>();
        foreach (var token in ExtractTokens(prompt, '#'))
        {
            var normalized = NormalizeReferenceToken(token, '#');
            if (IsSelectionToken(normalized))
            {
                var selection = GetCurrentSelectionReference(maxBytesPerReference);
                if (selection is not null)
                {
                    resolved.Add(selection);
                }

                continue;
            }

            resolved.AddRange(SearchFiles(normalized, 1).Select(f => new WorkspaceFileReference
            {
                Path = f.Path,
                RelativePath = f.RelativePath,
                Preview = SafePreview(f.Path, maxBytesPerReference),
                ReferenceKind = "file",
                ReferenceKey = FormatReferenceKey('#', f.RelativePath)
            }));
        }

        return resolved
            .GroupBy(x => string.IsNullOrWhiteSpace(x.ReferenceKey) ? x.Path : x.ReferenceKey, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }

    /// <summary>Gets current Selection Reference.</summary>
    /// <param name="maxChars">The max Chars.</param>
    /// <returns>The get Current Selection Reference result.</returns>
    private WorkspaceFileReference? GetCurrentSelectionReferenceCore(int maxChars)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var dte = _serviceProvider.GetService(typeof(DTE)) as DTE;
        var document = dte?.ActiveDocument;
        var selection = document?.Selection as TextSelection;
        var selectedText = selection?.Text ?? string.Empty;
        if (selection is null || string.IsNullOrWhiteSpace(selectedText))
        {
            return null;
        }

        var path = document?.FullName ?? string.Empty;
        var root = CurrentWorkspaceRoot;
        var relative = GetSelectionRelativePath(root, path);
        var startLine = Math.Min(selection.AnchorPoint.Line, selection.ActivePoint.Line);
        var endLine = Math.Max(selection.AnchorPoint.Line, selection.ActivePoint.Line);
        return CreateSelectionReference(path, relative, selectedText, maxChars, startLine, endLine);
    }

    /// <summary>Creates a reference for the active editor selection.</summary>
    /// <param name="path">The document path.</param>
    /// <param name="relativePath">The workspace-relative document path.</param>
    /// <param name="selectedText">The selected text.</param>
    /// <param name="maxChars">The maximum preview length.</param>
    /// <param name="startLine">The selection start line.</param>
    /// <param name="endLine">The selection end line.</param>
    /// <returns>The selection reference.</returns>
    private WorkspaceFileReference CreateSelectionReference(string path, string relativePath, string selectedText, int maxChars, int startLine, int endLine)
    {
        var preview = maxChars > 0 && selectedText.Length > maxChars
            ? selectedText.Remove(maxChars)
            : selectedText;
        return new WorkspaceFileReference
        {
            Path = path,
            RelativePath = relativePath,
            Preview = preview,
            ReferenceKind = SelectionText,
            ReferenceKey = $"#selection:{relativePath}:{startLine}-{endLine}",
            StartLine = startLine,
            EndLine = endLine
        };
    }

    /// <summary>Gets the selection path relative to the active workspace.</summary>
    /// <param name="root">The workspace root.</param>
    /// <param name="path">The document path.</param>
    /// <returns>The workspace-relative path when available; otherwise the file name.</returns>
    private string GetSelectionRelativePath(string root, string path)
    {
        return !string.IsNullOrWhiteSpace(root)
            && !string.IsNullOrWhiteSpace(path)
            && path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? MakeRelative(root, path)
            : Path.GetFileName(path);
    }

    /// <summary>Performs the extract Tokens operation.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <param name="marker">The marker.</param>
    /// <returns>The extract Tokens result.</returns>
    private IEnumerable<string> ExtractTokens(string prompt, char marker)
    {
        var text = prompt ?? string.Empty;
        var regex = marker == '#' ? HashReferenceRegex : AtReferenceRegex;

        return regex.Matches(text)
            .Cast<Match>()
            .Select(match => match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Groups["plain"].Value)
            .Select(value => marker + value.Trim().Trim(',', ';', '.', ')', ']', '}', ':'))
            .Where(x => x.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Numeric12);
    }

    /// <summary>Performs the matches Selection Query operation.</summary>
    /// <param name="selection">The selection.</param>
    /// <param name="term">The term.</param>
    /// <returns><see langword="true"/> when matches Selection Query succeeds; otherwise, <see langword="false"/>.</returns>
    private bool MatchesSelectionQuery(WorkspaceFileReference selection, string term)
    {
        return string.IsNullOrWhiteSpace(term)
            || SelectionText.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
            || "selected-code".IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
            || selection.RelativePath.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>Determines whether is Selection Token.</summary>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> when is Selection Token succeeds; otherwise, <see langword="false"/>.</returns>
    private bool IsSelectionToken(string token)
    {
        return token.Equals(SelectionText, StringComparison.OrdinalIgnoreCase)
            || token.Equals("selected", StringComparison.OrdinalIgnoreCase)
            || token.Equals("selected-code", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("selection:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Performs the normalize Reference Token operation.</summary>
    /// <param name="value">The value.</param>
    /// <param name="marker">The marker.</param>
    /// <returns>The normalize Reference Token result.</returns>
    private string NormalizeReferenceToken(string value, char marker)
    {
        var token = (value ?? string.Empty)
            .Trim()
            .TrimStart(marker)
            .Trim(',', ';', '.', ')', ']', '}', ':');
        if (token.Length >= Numeric2 && token[0] == '"' && token.EndsWith("\"", StringComparison.Ordinal))
        {
            token = token.Remove(token.LastIndexOf('"')).Remove(0, 1);
        }

        return token;
    }

    /// <summary>Performs the rebuild Workspace File Index operation.</summary>
    /// <param name="root">The root.</param>
    /// <param name="dte">The dte.</param>
    private void RebuildWorkspaceFileIndex(string root, DTE? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var indexed = new List<WorkspaceFileReference>();
        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
        {
            foreach (var path in SafeEnumerateFiles(root, MaxIndexedFiles))
            {
                if (IsSafeTextCandidate(path))
                {
                    indexed.Add(CreateFileReference(root, path, '@', includePreview: false));
                }
            }
        }

        foreach (var path in EnumerateSolutionItemFiles(dte))
        {
            if (indexed.Count >= MaxIndexedFiles)
            {
                break;
            }

            if (IsSafeTextCandidate(path) && !indexed.Any(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)))
            {
                indexed.Add(CreateFileReference(root, path, '@', includePreview: false));
            }
        }

        lock (_indexGate)
        {
            _workspaceFileIndex = indexed
                .OrderBy(x => x.RelativePath.Length)
                .ThenBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    /// <summary>Performs the snapshot Workspace File Index operation.</summary>
    /// <param name="root">The root.</param>
    /// <returns>The snapshot Workspace File Index result.</returns>
    private IReadOnlyList<WorkspaceFileReference> SnapshotWorkspaceFileIndex(string root)
    {
        lock (_indexGate)
        {
            if (_workspaceFileIndex.Count > 0 || string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return _workspaceFileIndex.ToList();
            }
        }

        var indexed = SafeEnumerateFiles(root, MaxIndexedFiles)
            .Where(IsSafeTextCandidate)
            .Select(path => CreateFileReference(root, path, '@', includePreview: false))
            .OrderBy(x => x.RelativePath.Length)
            .ThenBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        lock (_indexGate)
        {
            _workspaceFileIndex = indexed;
            return _workspaceFileIndex.ToList();
        }
    }

    /// <summary>Performs the search Explicit Path operation.</summary>
    /// <param name="term">The term.</param>
    /// <param name="limit">The limit.</param>
    /// <returns>The search Explicit Path result.</returns>
    private IReadOnlyList<WorkspaceFileReference> SearchExplicitPath(string term, int limit)
    {
        if (string.IsNullOrWhiteSpace(term) || !LooksLikePath(term))
        {
            return [];
        }

        var root = CurrentWorkspaceRoot;
        if (File.Exists(term))
        {
            return [WithPreview(CreateFileReference(root, term, '@', includePreview: false), Numeric2048, '@')];
        }

        var directory = Directory.Exists(term) ? term : Path.GetDirectoryName(term);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return [];
        }

        var leaf = Directory.Exists(term) ? string.Empty : Path.GetFileName(term);
        return SafeEnumerateFiles(directory, Math.Max(limit * Numeric4, limit), recursive: false)
            .Where(IsSafeTextCandidate)
            .Select(path => CreateFileReference(root, path, '@', includePreview: false))
            .Where(x => string.IsNullOrWhiteSpace(leaf)
                || Path.GetFileName(x.Path).IndexOf(leaf, StringComparison.OrdinalIgnoreCase) >= 0
                || x.Path.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(x => RankFileMatch(x, leaf))
            .ThenBy(x => x.RelativePath.Length)
            .Take(limit)
            .Select(x => WithPreview(x, Numeric2048, '@'))
            .ToList();
    }

    /// <summary>Performs the safe Enumerate Files operation.</summary>
    /// <param name="root">The root.</param>
    /// <param name="limit">The limit.</param>
    /// <param name="recursive">The recursive.</param>
    /// <returns>The safe Enumerate Files result.</returns>
    private IEnumerable<string> SafeEnumerateFiles(string root, int limit, bool recursive = true)
    {
        if (string.IsNullOrWhiteSpace(root) || limit <= 0)
        {
            yield break;
        }

        var pending = new Stack<string>();
        pending.Push(root);
        var count = 0;
        while (pending.Count > 0 && count < limit)
        {
            var current = pending.Pop();
            foreach (var file in EnumerateFilesOrEmpty(current))
            {
                if (count >= limit)
                {
                    yield break;
                }

                count++;
                yield return file;
            }

            if (!recursive)
            {
                continue;
            }

            foreach (var directory in EnumerateDirectoriesOrEmpty(current).Where(IsSearchableDirectory))
            {
                pending.Push(directory);
            }
        }
    }

    /// <summary>Enumerates files, returning no items if the directory cannot be read.</summary>
    /// <param name="directory">The directory to enumerate.</param>
    /// <returns>The files in the directory.</returns>
    private IEnumerable<string> EnumerateFilesOrEmpty(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory);
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Enumerates directories, returning no items if the directory cannot be read.</summary>
    /// <param name="directory">The directory to enumerate.</param>
    /// <returns>The directories in the directory.</returns>
    private IEnumerable<string> EnumerateDirectoriesOrEmpty(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory);
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Performs the enumerate Solution Item Files operation.</summary>
    /// <param name="dte">The dte.</param>
    /// <returns>The enumerate Solution Item Files result.</returns>
    private IEnumerable<string> EnumerateSolutionItemFiles(DTE? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var projects = dte?.Solution?.Projects;
        if (projects is null)
        {
            yield break;
        }

        foreach (Project project in projects)
        {
            foreach (var path in EnumerateProjectFiles(project))
            {
                yield return path;
            }
        }
    }

    /// <summary>Performs the enumerate Project Files operation.</summary>
    /// <param name="project">The project.</param>
    /// <returns>The enumerate Project Files result.</returns>
    private IEnumerable<string> EnumerateProjectFiles(Project project)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        foreach (var path in EnumerateProjectItemFiles(project.ProjectItems))
        {
            yield return path;
        }
    }

    /// <summary>Performs the enumerate Project Item Files operation.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The enumerate Project Item Files result.</returns>
    private IEnumerable<string> EnumerateProjectItemFiles(ProjectItems? items)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (items is null)
        {
            yield break;
        }

        foreach (ProjectItem item in items)
        {
            string? fileName = null;
            try
            {
                if (item.FileCount > 0)
                {
                    fileName = item.FileNames[1];
                }
            }
            catch
            {
                fileName = null;
            }

            if (!string.IsNullOrWhiteSpace(fileName) && File.Exists(fileName))
            {
                yield return fileName!;
            }

            foreach (var nested in EnumerateProjectItemFiles(item.ProjectItems))
            {
                yield return nested;
            }
        }
    }

    /// <summary>Creates file Reference.</summary>
    /// <param name="root">The root.</param>
    /// <param name="path">The path.</param>
    /// <param name="marker">The marker.</param>
    /// <param name="includePreview">The include Preview.</param>
    /// <returns>The create File Reference result.</returns>
    private WorkspaceFileReference CreateFileReference(string root, string path, char marker, bool includePreview)
    {
        var relative = !string.IsNullOrWhiteSpace(root) && path.StartsWith(AppendSlash(root), StringComparison.OrdinalIgnoreCase)
            ? MakeRelative(root, path)
            : path;

        return new WorkspaceFileReference
        {
            Path = path,
            RelativePath = relative,
            Preview = includePreview ? SafePreview(path, Numeric2048) : string.Empty,
            ReferenceKind = "file",
            ReferenceKey = FormatReferenceKey(marker, relative)
        };
    }

    /// <summary>Performs the with Preview operation.</summary>
    /// <param name="reference">The reference.</param>
    /// <param name="maxBytes">The max Bytes.</param>
    /// <param name="marker">The marker.</param>
    /// <returns>The with Preview result.</returns>
    private WorkspaceFileReference WithPreview(WorkspaceFileReference reference, int maxBytes, char marker)
    {
        return new WorkspaceFileReference
        {
            Path = reference.Path,
            RelativePath = reference.RelativePath,
            Preview = SafePreview(reference.Path, maxBytes),
            ReferenceKind = reference.ReferenceKind,
            ReferenceKey = FormatReferenceKey(marker, reference.RelativePath),
            StartLine = reference.StartLine,
            EndLine = reference.EndLine
        };
    }

    /// <summary>Performs the rank File Match operation.</summary>
    /// <param name="reference">The reference.</param>
    /// <param name="term">The term.</param>
    /// <returns>The rank File Match result.</returns>
    private int RankFileMatch(WorkspaceFileReference reference, string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return reference.RelativePath.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar);
        }

        var fileName = Path.GetFileName(reference.Path);
        if (fileName.Equals(term, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (fileName.StartsWith(term, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return reference.RelativePath.StartsWith(term, StringComparison.OrdinalIgnoreCase) ? Numeric2 : Numeric3;
    }

    /// <summary>Performs the looks Like Path operation.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when looks Like Path succeeds; otherwise, <see langword="false"/>.</returns>
    private bool LooksLikePath(string value)
    {
        return Path.IsPathRooted(value)
            || value.IndexOf(Path.DirectorySeparatorChar) >= 0
            || value.IndexOf(Path.AltDirectorySeparatorChar) >= 0;
    }

    /// <summary>Determines whether is Searchable Directory.</summary>
    /// <param name="path">The path.</param>
    /// <returns><see langword="true"/> when is Searchable Directory succeeds; otherwise, <see langword="false"/>.</returns>
    private bool IsSearchableDirectory(string path)
    {
        var name = Path.GetFileName(path);
        return !new[] { ".git", ".vs", "bin", "obj", "node_modules", "packages" }.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Determines whether is Safe Text Candidate.</summary>
    /// <param name="p">The p.</param>
    /// <returns><see langword="true"/> when is Safe Text Candidate succeeds; otherwise, <see langword="false"/>.</returns>
    private bool IsSafeTextCandidate(string p)
    {
        return !IsExcludedPath(p) && IsSupportedTextExtension(p);
    }

    /// <summary>Determines whether path is excluded from indexing.</summary>
    /// <param name="path">The path.</param>
    /// <returns><see langword="true"/> when path is excluded; otherwise, <see langword="false"/>.</returns>
    private bool IsExcludedPath(string path)
    {
        var lower = path.ToLowerInvariant();
        return lower.Contains("\\bin\\")
            || lower.Contains("\\obj\\")
            || lower.Contains("\\.git\\")
            || lower.Contains("\\.vs\\")
            || lower.Contains("\\node_modules\\")
            || lower.Contains("/bin/")
            || lower.Contains("/obj/")
            || lower.Contains("/.git/")
            || lower.Contains("/.vs/")
            || lower.Contains("/node_modules/");
    }

    /// <summary>Determines whether path has a supported text extension.</summary>
    /// <param name="path">The path.</param>
    /// <returns><see langword="true"/> when the extension is supported; otherwise, <see langword="false"/>.</returns>
    private bool IsSupportedTextExtension(string path)
    {
        return new[]
        {
            ".cs", ".csx", ".xaml", ".xml", ".json", ".jsonc", ".md", ".props", ".targets", ".sln", ".slnx", ".csproj", ".config", ".toml", ".txt",
            ".editorconfig", ".ruleset", ".resx", ".settings", ".ps1", ".psm1", ".cmd", ".bat", ".sh", ".yml", ".yaml", ".ini", ".sql",
            ".js", ".jsx", ".ts", ".tsx", ".css", ".scss", ".html", ".htm", ".razor", ".vb", ".fs", ".fsx", ".cpp", ".h", ".hpp"
        }.Contains(Path.GetExtension(path).ToLowerInvariant());
    }

    /// <summary>Formats reference Key.</summary>
    /// <param name="marker">The marker.</param>
    /// <param name="path">The path.</param>
    /// <returns>The format Reference Key result.</returns>
    private string FormatReferenceKey(char marker, string path)
    {
        var value = path ?? string.Empty;
        if (value.IndexOfAny([' ', '\t', '\r', '\n']) >= 0)
        {
            value = $"\"{value.Replace("\"", "\\\"")}\"";
        }

        return marker + value;
    }

    /// <summary>Performs the safe Preview operation.</summary>
    /// <param name="path">The path.</param>
    /// <param name="maxBytes">The max Bytes.</param>
    /// <returns>The safe Preview result.</returns>
    private string SafePreview(string path, int maxBytes)
    {
        try
        {
            using (var fs = File.OpenRead(path))
            using (var sr = new StreamReader(fs))
            {
                var buffer = new char[Math.Max(1, maxBytes)];
                var read = sr.Read(buffer, 0, buffer.Length);
                return new(buffer, 0, read);
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Performs the make Relative operation.</summary>
    /// <param name="root">The root.</param>
    /// <param name="path">The path.</param>
    /// <returns>The make Relative result.</returns>
    private string MakeRelative(string root, string path)
    {
        var uri = new Uri(AppendSlash(root));
        var file = new Uri(path);
        return Uri.UnescapeDataString(uri.MakeRelativeUri(file).ToString()).Replace('/', Path.DirectorySeparatorChar);
    }

    /// <summary>Performs the append Slash operation.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The append Slash result.</returns>
    private string AppendSlash(string path) => path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;

    /// <summary>Gets solution Path.</summary>
    /// <param name="dte">The dte.</param>
    /// <returns>The get Solution Path result.</returns>
    private string GetSolutionPath(DTE? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var dteSolution = dte?.Solution?.FullName ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(dteSolution))
        {
            return File.Exists(dteSolution) ? dteSolution : string.Empty;
        }

        return TryGetSolutionInfo(out var directory, out var file)
            ? GetSolutionFilePath(directory, file)
            : string.Empty;
    }

    /// <summary>Gets open Folder Directory.</summary>
    /// <param name="dte">The dte.</param>
    /// <returns>The get Open Folder Directory result.</returns>
    private string GetOpenFolderDirectory(DTE? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var dteSolution = dte?.Solution?.FullName ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(dteSolution) && Directory.Exists(dteSolution))
        {
            return dteSolution;
        }

        return TryGetSolutionInfo(out var directory, out var file)
            && string.IsNullOrWhiteSpace(file)
            && !string.IsNullOrWhiteSpace(directory)
            && Directory.Exists(directory)
            ? directory
            : string.Empty;
    }

    /// <summary>Gets solution information from the Visual Studio shell.</summary>
    /// <param name="directory">The solution directory.</param>
    /// <param name="file">The solution file.</param>
    /// <returns><see langword="true"/> when solution information is available; otherwise, <see langword="false"/>.</returns>
    private bool TryGetSolutionInfo(out string directory, out string file)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        directory = string.Empty;
        file = string.Empty;
        try
        {
            object? service = _serviceProvider.GetService(typeof(SVsSolution));
            return service is IVsSolution solution
                && Microsoft.VisualStudio.ErrorHandler.Succeeded(solution.GetSolutionInfo(out directory, out file, out _));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Gets the full solution path from Visual Studio solution information.</summary>
    /// <param name="directory">The solution directory.</param>
    /// <param name="file">The solution file.</param>
    /// <returns>The full solution path when it can be resolved; otherwise an empty string.</returns>
    private string GetSolutionFilePath(string directory, string file)
    {
        if (!string.IsNullOrWhiteSpace(file) && Path.IsPathRooted(file))
        {
            return file;
        }

        return !string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(file)
            ? Path.Combine(directory, file)
            : string.Empty;
    }

    /// <summary>Resolves workspace Start Directory.</summary>
    /// <param name="solutionPath">The solution Path.</param>
    /// <param name="openFolderDirectory">The open Folder Directory.</param>
    /// <param name="activeProjectDirectory">The active Project Directory.</param>
    /// <param name="activeDocumentDirectory">The active Document Directory.</param>
    /// <returns>The resolve Workspace Start Directory result.</returns>
    private string ResolveWorkspaceStartDirectory(string solutionPath, string openFolderDirectory, string activeProjectDirectory, string activeDocumentDirectory)
    {
        if (!string.IsNullOrWhiteSpace(solutionPath))
        {
            var solutionDirectory = Path.GetDirectoryName(solutionPath);
            if (!string.IsNullOrWhiteSpace(solutionDirectory))
            {
                return solutionDirectory;
            }
        }

        if (!string.IsNullOrWhiteSpace(openFolderDirectory))
        {
            return openFolderDirectory;
        }

        return !string.IsNullOrWhiteSpace(activeProjectDirectory) ? activeProjectDirectory : activeDocumentDirectory;
    }

    /// <summary>Resolves workspace Root.</summary>
    /// <param name="startDirectory">The start Directory.</param>
    /// <returns>The resolve Workspace Root result.</returns>
    private string ResolveWorkspaceRoot(string startDirectory)
    {
        return string.IsNullOrWhiteSpace(startDirectory) || !Directory.Exists(startDirectory) ? string.Empty : FindRepositoryRoot(startDirectory) ?? startDirectory;
    }

    /// <summary>Finds repository Root.</summary>
    /// <param name="startDirectory">The start Directory.</param>
    /// <returns>The find Repository Root result.</returns>
    private string? FindRepositoryRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            var gitPath = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    /// <summary>Builds workspace Name.</summary>
    /// <param name="workspaceRoot">The workspace Root.</param>
    /// <param name="solutionPath">The solution Path.</param>
    /// <returns>The build Workspace Name result.</returns>
    private string BuildWorkspaceName(string workspaceRoot, string solutionPath)
    {
        if (!string.IsNullOrWhiteSpace(workspaceRoot))
        {
            return Path.GetFileName(workspaceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        return string.IsNullOrWhiteSpace(solutionPath) ? "VSCodex workspace" : Path.GetFileNameWithoutExtension(solutionPath);
    }

    /// <summary>Builds workspace Identity.</summary>
    /// <param name="workspaceRoot">The workspace Root.</param>
    /// <param name="solutionPath">The solution Path.</param>
    /// <returns>The build Workspace Identity result.</returns>
    private WorkspaceIdentity BuildWorkspaceIdentity(string workspaceRoot, string solutionPath)
    {
        var solutionRelativePath = MakeRelativeIfContained(workspaceRoot, solutionPath);
        var repositoryRemote = ReadRepositoryRemote(workspaceRoot);
        var name = BuildWorkspaceName(workspaceRoot, solutionPath);
        var id = string.IsNullOrWhiteSpace(workspaceRoot) && string.IsNullOrWhiteSpace(solutionPath)
            ? string.Empty
            : ComputeWorkspaceIdentityId(repositoryRemote, workspaceRoot);

        return new WorkspaceIdentity
        {
            Id = id,
            Name = name,
            RootPath = workspaceRoot,
            SolutionPath = solutionPath,
            SolutionRelativePath = solutionRelativePath,
            RepositoryRemote = repositoryRemote,
            MemoryRoot = BuildWorkspaceMemoryRoot(id)
        };
    }

    /// <summary>Gets active Project Directory.</summary>
    /// <param name="dte">The dte.</param>
    /// <returns>The get Active Project Directory result.</returns>
    private string GetActiveProjectDirectory(DTE? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            if (dte?.ActiveSolutionProjects is Array projects)
            {
                foreach (var item in projects)
                {
                    if (item is Project project)
                    {
                        var directory = ProjectPathToDirectory(project.FullName);
                        if (!string.IsNullOrWhiteSpace(directory))
                        {
                            return directory;
                        }
                    }
                }
            }
        }
        catch
        {
            return string.Empty;
        }

        return string.Empty;
    }

    /// <summary>Gets active Document Directory.</summary>
    /// <param name="dte">The dte.</param>
    /// <returns>The get Active Document Directory result.</returns>
    private string GetActiveDocumentDirectory(DTE? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            var path = dte?.ActiveDocument?.FullName ?? string.Empty;
            return ProjectPathToDirectory(path);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Performs the project Path To Directory operation.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The project Path To Directory result.</returns>
    private string ProjectPathToDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? string.Empty;
    }

    /// <summary>Performs the make Relative If Contained operation.</summary>
    /// <param name="root">The root.</param>
    /// <param name="path">The path.</param>
    /// <returns>The make Relative If Contained result.</returns>
    private string MakeRelativeIfContained(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
        {
            return string.Empty;
        }

        try
        {
            var fullRoot = Path.GetFullPath(AppendSlash(root));
            var fullPath = Path.GetFullPath(path);
            return !fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) ? string.Empty : MakeRelative(fullRoot, fullPath);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Reads repository Remote.</summary>
    /// <param name="workspaceRoot">The workspace Root.</param>
    /// <returns>The read Repository Remote result.</returns>
    private string ReadRepositoryRemote(string workspaceRoot)
    {
        var configPath = ResolveGitConfigPath(workspaceRoot);
        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
        {
            return string.Empty;
        }

        try
        {
            var config = File.ReadAllText(configPath);
            var origin = new Regex("""(?ms)^\s*\[remote\s+"origin"\]\s*(.*?)(?=^\s*\[|\z)""").Match(config);
            var body = origin.Success ? origin.Groups[1].Value : config;
            var url = new Regex(@"(?m)^\s*url\s*=\s*(.+?)\s*$").Match(body);
            return url.Success ? url.Groups[1].Value.Trim() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Resolves git Config Path.</summary>
    /// <param name="workspaceRoot">The workspace Root.</param>
    /// <returns>The resolve Git Config Path result.</returns>
    private string ResolveGitConfigPath(string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            return string.Empty;
        }

        var gitPath = Path.Combine(workspaceRoot, ".git");
        if (Directory.Exists(gitPath))
        {
            return Path.Combine(gitPath, "config");
        }

        if (!File.Exists(gitPath))
        {
            return string.Empty;
        }

        try
        {
            var gitFile = File.ReadAllText(gitPath).Trim();
            const string prefix = "gitdir:";
            if (!gitFile.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var gitDirectory = gitFile.Substring(prefix.Length).Trim();
            if (!Path.IsPathRooted(gitDirectory))
            {
                gitDirectory = Path.GetFullPath(Path.Combine(workspaceRoot, gitDirectory));
            }

            return Path.Combine(gitDirectory, "config");
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Computes workspace Identity Id.</summary>
    /// <param name="parts">The parts.</param>
    /// <returns>The compute Workspace Identity Id result.</returns>
    private string ComputeWorkspaceIdentityId(params string[] parts)
    {
        var key = string.Join("|", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(NormalizeIdentityPart));
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        using var sha = SHA256.Create();
        return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(key)), Numeric12);
    }

    /// <summary>Builds workspace Memory Root.</summary>
    /// <param name="workspaceIdentityId">The workspace Identity Id.</param>
    /// <returns>The build Workspace Memory Root result.</returns>
    private string BuildWorkspaceMemoryRoot(string workspaceIdentityId)
        => string.IsNullOrWhiteSpace(workspaceIdentityId) ? string.Empty : $"reactivememory://workspace/{workspaceIdentityId}";

    /// <summary>Performs the normalize Identity Part operation.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The normalize Identity Part result.</returns>
    private string NormalizeIdentityPart(string value) => value.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Trim().ToLowerInvariant();

    /// <summary>Performs the to Hex operation.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="byteCount">The byte Count.</param>
    /// <returns>The to Hex result.</returns>
    private string ToHex(byte[] bytes, int byteCount)
    {
        var builder = new StringBuilder(byteCount * Numeric2);
        for (var i = 0; i < Math.Min(bytes.Length, byteCount); i++)
        {
            _ = builder.Append(bytes[i].ToString("x2"));
        }

        return builder.ToString();
    }
}
