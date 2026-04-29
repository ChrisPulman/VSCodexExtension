using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using VSCodexExtension.Models;
namespace VSCodexExtension.Services
{
    public interface ISkillIndexService { IObservable<IReadOnlyList<SkillDefinition>> Skills { get; } IReadOnlyList<SkillDefinition> Snapshot { get; } void Refresh(IEnumerable<string> roots); }
    public sealed class SkillIndexService : ISkillIndexService
    {
        private readonly BehaviorSubject<IReadOnlyList<SkillDefinition>> _skills = new BehaviorSubject<IReadOnlyList<SkillDefinition>>(Array.Empty<SkillDefinition>());
        public IObservable<IReadOnlyList<SkillDefinition>> Skills => _skills.AsObservable();
        public IReadOnlyList<SkillDefinition> Snapshot => _skills.Value;
        public void Refresh(IEnumerable<string> roots)
        {
            var results = new List<SkillDefinition>();
            foreach (var root in roots.Where(Directory.Exists))
            foreach (var file in Directory.EnumerateFiles(root, "SKILL.md", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(file);
                results.Add(new SkillDefinition { Name = ReadFrontmatter(content, "name") ?? new DirectoryInfo(Path.GetDirectoryName(file)!).Name, Description = ReadFrontmatter(content, "description") ?? FirstParagraph(content), RootPath = Path.GetDirectoryName(file)!, MarkdownPath = file, Content = content });
            }
            _skills.OnNext(results.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList());
        }
        private static string? ReadFrontmatter(string content, string key)
        {
            var match = Regex.Match(content, "^---\\s*(.*?)\\s*---", RegexOptions.Singleline); if (!match.Success) return null;
            foreach (var line in match.Groups[1].Value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            { var parts = line.Split(new[] { ':' }, 2); if (parts.Length == 2 && parts[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) return parts[1].Trim().Trim('"'); }
            return null;
        }
        private static string FirstParagraph(string content) => content.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault()?.Trim() ?? string.Empty;
    }
}
