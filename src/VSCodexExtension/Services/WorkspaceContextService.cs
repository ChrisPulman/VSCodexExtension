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
        IReadOnlyList<WorkspaceFileReference> ResolveMentions(string prompt, int maxBytesPerFile);
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
            var root = CurrentWorkspaceRoot; if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return Array.Empty<WorkspaceFileReference>();
            var term = (query ?? string.Empty).Trim().TrimStart('@');
            return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(IsSafeTextCandidate)
                .Select(p => new { Path = p, Relative = MakeRelative(root, p) })
                .Where(x => string.IsNullOrWhiteSpace(term) || x.Relative.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(x => x.Relative.Length).Take(limit)
                .Select(x => new WorkspaceFileReference { Path = x.Path, RelativePath = x.Relative, Preview = SafePreview(x.Path, 2048) }).ToList();
        }
        public IReadOnlyList<WorkspaceFileReference> ResolveMentions(string prompt, int maxBytesPerFile)
        {
            var mentions = (prompt ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(x => x.StartsWith("@", StringComparison.Ordinal) && x.Length > 1).Select(x => x.TrimStart('@').Trim(',', ';', '.', ')', ']')).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList();
            return mentions.SelectMany(m => SearchFiles(m, 1)).Select(f => new WorkspaceFileReference { Path = f.Path, RelativePath = f.RelativePath, Preview = SafePreview(f.Path, maxBytesPerFile) }).ToList();
        }
        private static bool IsSafeTextCandidate(string p)
        {
            var lower = p.ToLowerInvariant(); if (lower.Contains("\\bin\\") || lower.Contains("\\obj\\") || lower.Contains("\\.git\\") || lower.Contains("/bin/") || lower.Contains("/obj/") || lower.Contains("/.git/")) return false;
            return new[] { ".cs", ".xaml", ".xml", ".json", ".md", ".props", ".targets", ".sln", ".csproj", ".config", ".toml", ".txt" }.Contains(Path.GetExtension(p).ToLowerInvariant());
        }
        private static string SafePreview(string path, int maxBytes) { try { using (var fs = File.OpenRead(path)) using (var sr = new StreamReader(fs)) { var buffer = new char[Math.Max(1, maxBytes)]; var read = sr.Read(buffer, 0, buffer.Length); return new string(buffer, 0, read); } } catch { return string.Empty; } }
        private static string MakeRelative(string root, string path) { var uri = new Uri(AppendSlash(root)); var file = new Uri(path); return Uri.UnescapeDataString(uri.MakeRelativeUri(file).ToString()).Replace('/', Path.DirectorySeparatorChar); }
        private static string AppendSlash(string path) => path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;
    }
}
