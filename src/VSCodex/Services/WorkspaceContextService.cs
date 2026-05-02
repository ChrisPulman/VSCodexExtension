using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using VSCodex.Models;

namespace VSCodex.Services;

public interface IWorkspaceContextService
{
    IObservable<string> WorkspaceRoot { get; }
    string CurrentWorkspaceRoot { get; }
    void Refresh();
    IReadOnlyList<WorkspaceFileReference> SearchFiles(string query, int limit);
    IReadOnlyList<WorkspaceFileReference> SearchContextReferences(string query, int limit);
    IReadOnlyList<WorkspaceFileReference> ResolveMentions(string prompt, int maxBytesPerFile);
    IReadOnlyList<WorkspaceFileReference> ResolveHashReferences(string prompt, int maxBytesPerReference);
    WorkspaceFileReference? GetCurrentSelectionReference(int maxChars);
}

public sealed class WorkspaceContextService : IWorkspaceContextService
{
    private const int MaxIndexedFiles = 5000;
    private readonly IServiceProvider _serviceProvider;
    private readonly BehaviorSubject<string> _workspaceRoot = new BehaviorSubject<string>(string.Empty);
    private readonly object _indexGate = new object();
    private List<WorkspaceFileReference> _workspaceFileIndex = new List<WorkspaceFileReference>();

    public WorkspaceContextService(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public IObservable<string> WorkspaceRoot => _workspaceRoot.AsObservable();

    public string CurrentWorkspaceRoot => _workspaceRoot.Value;

    public void Refresh()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var dte = _serviceProvider.GetService(typeof(DTE)) as DTE;
        var sln = dte?.Solution?.FullName;
        var root = string.IsNullOrWhiteSpace(sln) ? string.Empty : Path.GetDirectoryName(sln) ?? string.Empty;
        _workspaceRoot.OnNext(root);
        RebuildWorkspaceFileIndex(root, dte);
    }

    public IReadOnlyList<WorkspaceFileReference> SearchFiles(string query, int limit)
    {
        if (limit <= 0)
        {
            return Array.Empty<WorkspaceFileReference>();
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
            .Select(x => WithPreview(x, 2048, '@'))
            .ToList();
    }

    public IReadOnlyList<WorkspaceFileReference> SearchContextReferences(string query, int limit)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (limit <= 0)
        {
            return Array.Empty<WorkspaceFileReference>();
        }

        var term = NormalizeReferenceToken(query, '#');
        var results = new List<WorkspaceFileReference>();
        var selection = GetCurrentSelectionReference(12000);
        if (selection != null && MatchesSelectionQuery(selection, term))
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
                    ReferenceKey = "#" + file.RelativePath
                });
            }
        }

        return results;
    }

    public IReadOnlyList<WorkspaceFileReference> ResolveMentions(string prompt, int maxBytesPerFile)
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

    public IReadOnlyList<WorkspaceFileReference> ResolveHashReferences(string prompt, int maxBytesPerReference)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var resolved = new List<WorkspaceFileReference>();
        foreach (var token in ExtractTokens(prompt, '#'))
        {
            var normalized = NormalizeReferenceToken(token, '#');
            if (IsSelectionToken(normalized))
            {
                var selection = GetCurrentSelectionReference(maxBytesPerReference);
                if (selection != null)
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

    public WorkspaceFileReference? GetCurrentSelectionReference(int maxChars)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var dte = _serviceProvider.GetService(typeof(DTE)) as DTE;
        var document = dte?.ActiveDocument;
        var selection = document?.Selection as TextSelection;
        var selectedText = selection?.Text ?? string.Empty;
        if (selection == null || string.IsNullOrWhiteSpace(selectedText))
        {
            return null;
        }

        var path = document?.FullName ?? string.Empty;
        var root = CurrentWorkspaceRoot;
        var relative = !string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(path) && path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? MakeRelative(root, path)
            : Path.GetFileName(path);
        var startLine = Math.Min(selection.AnchorPoint.Line, selection.ActivePoint.Line);
        var endLine = Math.Max(selection.AnchorPoint.Line, selection.ActivePoint.Line);
        var preview = maxChars > 0 && selectedText.Length > maxChars ? selectedText.Substring(0, Math.Max(0, maxChars)) : selectedText;

        return new WorkspaceFileReference
        {
            Path = path,
            RelativePath = relative,
            Preview = preview,
            ReferenceKind = "selection",
            ReferenceKey = $"#selection:{relative}:{startLine}-{endLine}",
            StartLine = startLine,
            EndLine = endLine
        };
    }

    private static IEnumerable<string> ExtractTokens(string prompt, char marker)
    {
        var text = prompt ?? string.Empty;
        var pattern = marker == '#'
            ? @"(?<!#)#(?:""(?<quoted>[^""]+)""|(?<plain>[^\s,;\)\]\}]+))"
            : @"@(?:""(?<quoted>[^""]+)""|(?<plain>[^\s,;\)\]\}]+))";

        return Regex.Matches(text, pattern)
            .Cast<Match>()
            .Select(match => match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Groups["plain"].Value)
            .Select(value => marker + value.Trim().Trim(',', ';', '.', ')', ']', '}', ':'))
            .Where(x => x.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12);
    }

    private static bool MatchesSelectionQuery(WorkspaceFileReference selection, string term)
    {
        return string.IsNullOrWhiteSpace(term)
            || "selection".IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
            || "selected-code".IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
            || selection.RelativePath.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsSelectionToken(string token)
    {
        return token.Equals("selection", StringComparison.OrdinalIgnoreCase)
            || token.Equals("selected", StringComparison.OrdinalIgnoreCase)
            || token.Equals("selected-code", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("selection:", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeReferenceToken(string value, char marker)
    {
        var token = (value ?? string.Empty)
            .Trim()
            .TrimStart(marker)
            .Trim(',', ';', '.', ')', ']', '}', ':');
        if (token.Length >= 2 && token[0] == '"' && token[token.Length - 1] == '"')
        {
            token = token.Substring(1, token.Length - 2);
        }

        return token;
    }

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

    private IReadOnlyList<WorkspaceFileReference> SearchExplicitPath(string term, int limit)
    {
        if (string.IsNullOrWhiteSpace(term) || !LooksLikePath(term))
        {
            return Array.Empty<WorkspaceFileReference>();
        }

        var root = CurrentWorkspaceRoot;
        if (File.Exists(term))
        {
            return new[] { WithPreview(CreateFileReference(root, term, '@', includePreview: false), 2048, '@') };
        }

        var directory = Directory.Exists(term) ? term : Path.GetDirectoryName(term);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return Array.Empty<WorkspaceFileReference>();
        }

        var leaf = Directory.Exists(term) ? string.Empty : Path.GetFileName(term);
        return SafeEnumerateFiles(directory, Math.Max(limit * 4, limit), recursive: false)
            .Where(IsSafeTextCandidate)
            .Select(path => CreateFileReference(root, path, '@', includePreview: false))
            .Where(x => string.IsNullOrWhiteSpace(leaf)
                || Path.GetFileName(x.Path).IndexOf(leaf, StringComparison.OrdinalIgnoreCase) >= 0
                || x.Path.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(x => RankFileMatch(x, leaf))
            .ThenBy(x => x.RelativePath.Length)
            .Take(limit)
            .Select(x => WithPreview(x, 2048, '@'))
            .ToList();
    }

    private static IEnumerable<string> SafeEnumerateFiles(string root, int limit, bool recursive = true)
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
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
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

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(current);
            }
            catch
            {
                continue;
            }

            foreach (var directory in directories.Where(IsSearchableDirectory))
            {
                pending.Push(directory);
            }
        }
    }

    private static IEnumerable<string> EnumerateSolutionItemFiles(DTE? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var projects = dte?.Solution?.Projects;
        if (projects == null)
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

    private static IEnumerable<string> EnumerateProjectFiles(Project project)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        foreach (var path in EnumerateProjectItemFiles(project.ProjectItems))
        {
            yield return path;
        }
    }

    private static IEnumerable<string> EnumerateProjectItemFiles(ProjectItems? items)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (items == null)
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

    private static WorkspaceFileReference CreateFileReference(string root, string path, char marker, bool includePreview)
    {
        var relative = !string.IsNullOrWhiteSpace(root) && path.StartsWith(AppendSlash(root), StringComparison.OrdinalIgnoreCase)
            ? MakeRelative(root, path)
            : path;

        return new WorkspaceFileReference
        {
            Path = path,
            RelativePath = relative,
            Preview = includePreview ? SafePreview(path, 2048) : string.Empty,
            ReferenceKind = "file",
            ReferenceKey = FormatReferenceKey(marker, relative)
        };
    }

    private static WorkspaceFileReference WithPreview(WorkspaceFileReference reference, int maxBytes, char marker)
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

    private static int RankFileMatch(WorkspaceFileReference reference, string term)
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

        if (reference.RelativePath.StartsWith(term, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 3;
    }

    private static bool LooksLikePath(string value)
    {
        return Path.IsPathRooted(value)
            || value.IndexOf(Path.DirectorySeparatorChar) >= 0
            || value.IndexOf(Path.AltDirectorySeparatorChar) >= 0;
    }

    private static bool IsSearchableDirectory(string path)
    {
        var name = Path.GetFileName(path);
        return !new[] { ".git", ".vs", "bin", "obj", "node_modules", "packages" }.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsSafeTextCandidate(string p)
    {
        var lower = p.ToLowerInvariant();
        if (lower.Contains("\\bin\\") || lower.Contains("\\obj\\") || lower.Contains("\\.git\\") || lower.Contains("\\.vs\\") || lower.Contains("\\node_modules\\") || lower.Contains("/bin/") || lower.Contains("/obj/") || lower.Contains("/.git/") || lower.Contains("/.vs/") || lower.Contains("/node_modules/"))
        {
            return false;
        }

        return new[]
        {
            ".cs", ".csx", ".xaml", ".xml", ".json", ".jsonc", ".md", ".props", ".targets", ".sln", ".slnx", ".csproj", ".config", ".toml", ".txt",
            ".editorconfig", ".ruleset", ".resx", ".settings", ".ps1", ".psm1", ".cmd", ".bat", ".sh", ".yml", ".yaml", ".ini", ".sql",
            ".js", ".jsx", ".ts", ".tsx", ".css", ".scss", ".html", ".htm", ".razor", ".vb", ".fs", ".fsx", ".cpp", ".h", ".hpp"
        }.Contains(Path.GetExtension(p).ToLowerInvariant());
    }

    private static string FormatReferenceKey(char marker, string path)
    {
        var value = path ?? string.Empty;
        if (value.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) >= 0)
        {
            value = "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        return marker + value;
    }

    private static string SafePreview(string path, int maxBytes)
    {
        try
        {
            using (var fs = File.OpenRead(path))
            using (var sr = new StreamReader(fs))
            {
                var buffer = new char[Math.Max(1, maxBytes)];
                var read = sr.Read(buffer, 0, buffer.Length);
                return new string(buffer, 0, read);
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string MakeRelative(string root, string path)
    {
        var uri = new Uri(AppendSlash(root));
        var file = new Uri(path);
        return Uri.UnescapeDataString(uri.MakeRelativeUri(file).ToString()).Replace('/', Path.DirectorySeparatorChar);
    }

    private static string AppendSlash(string path) => path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;
}
