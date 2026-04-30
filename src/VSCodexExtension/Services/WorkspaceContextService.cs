using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using VSCodexExtension.Models;

namespace VSCodexExtension.Services
{
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
        private readonly IServiceProvider _serviceProvider;
        private readonly BehaviorSubject<string> _workspaceRoot = new BehaviorSubject<string>(string.Empty);

        public WorkspaceContextService(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

        public IObservable<string> WorkspaceRoot => _workspaceRoot.AsObservable();

        public string CurrentWorkspaceRoot => _workspaceRoot.Value;

        public void Refresh()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = _serviceProvider.GetService(typeof(DTE)) as DTE;
            var sln = dte?.Solution?.FullName;
            _workspaceRoot.OnNext(string.IsNullOrWhiteSpace(sln) ? string.Empty : Path.GetDirectoryName(sln) ?? string.Empty);
        }

        public IReadOnlyList<WorkspaceFileReference> SearchFiles(string query, int limit)
        {
            var root = CurrentWorkspaceRoot;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root) || limit <= 0)
            {
                return Array.Empty<WorkspaceFileReference>();
            }

            var term = NormalizeReferenceToken(query, '@');
            return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(IsSafeTextCandidate)
                .Select(p => new { Path = p, Relative = MakeRelative(root, p) })
                .Where(x => string.IsNullOrWhiteSpace(term) || x.Relative.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(x => x.Relative.Length)
                .Take(limit)
                .Select(x => new WorkspaceFileReference
                {
                    Path = x.Path,
                    RelativePath = x.Relative,
                    Preview = SafePreview(x.Path, 2048),
                    ReferenceKind = "file",
                    ReferenceKey = "@" + x.Relative
                })
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
                    ReferenceKey = "@" + f.RelativePath
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
                    ReferenceKey = "#" + f.RelativePath
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
            var preview = selectedText.Length > maxChars ? selectedText.Substring(0, Math.Max(0, maxChars)) : selectedText;

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
            return (prompt ?? string.Empty)
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(x => x.Length > 1 && x[0] == marker && (marker != '#' || x.Length == 1 || x[1] != '#'))
                .Select(x => x.Trim().Trim(',', ';', '.', ')', ']', '}', ':'))
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
            return (value ?? string.Empty)
                .Trim()
                .TrimStart(marker)
                .Trim(',', ';', '.', ')', ']', '}', ':');
        }

        private static bool IsSafeTextCandidate(string p)
        {
            var lower = p.ToLowerInvariant();
            if (lower.Contains("\\bin\\") || lower.Contains("\\obj\\") || lower.Contains("\\.git\\") || lower.Contains("/bin/") || lower.Contains("/obj/") || lower.Contains("/.git/"))
            {
                return false;
            }

            return new[] { ".cs", ".xaml", ".xml", ".json", ".md", ".props", ".targets", ".sln", ".slnx", ".csproj", ".config", ".toml", ".txt" }.Contains(Path.GetExtension(p).ToLowerInvariant());
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
}
