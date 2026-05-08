using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using VSCodex.Models;

namespace VSCodex.Services;

public interface ISkillIndexService
{
    IObservable<IReadOnlyList<SkillDefinition>> Skills { get; }
    IReadOnlyList<SkillDefinition> Snapshot { get; }
    void Refresh(IEnumerable<string> roots);
    string CreateSkill(string root, string name, string description);
}
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

    public string CreateSkill(string root, string name, string description)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new ArgumentException("A skill root folder is required.", nameof(root));
        }

        var normalizedName = NormalizeSkillName(name);
        var skillRoot = Path.Combine(root, normalizedName);
        Directory.CreateDirectory(skillRoot);
        var skillPath = Path.Combine(skillRoot, "SKILL.md");
        if (!File.Exists(skillPath))
        {
            File.WriteAllText(skillPath, BuildSkillTemplate(normalizedName, description), new System.Text.UTF8Encoding(false));
        }

        return skillPath;
    }

    private static string? ReadFrontmatter(string content, string key)
    {
        var match = Regex.Match(content, "^---\\s*(.*?)\\s*---", RegexOptions.Singleline); if (!match.Success) return null;
        foreach (var line in match.Groups[1].Value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        { var parts = line.Split(new[] { ':' }, 2); if (parts.Length == 2 && parts[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) return parts[1].Trim().Trim('"'); }
        return null;
    }
    private static string FirstParagraph(string content) => content.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault()?.Trim() ?? string.Empty;

    private static string NormalizeSkillName(string name)
    {
        var value = (name ?? string.Empty).Trim();
        if (value.Length == 0 || !char.IsLetterOrDigit(value[0]) || value.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '-')))
        {
            throw new ArgumentException("Skill names must start with a letter or digit and can contain letters, digits, '.', '_' or '-'.", nameof(name));
        }

        return value;
    }

    private static string BuildSkillTemplate(string name, string description)
    {
        var summary = string.IsNullOrWhiteSpace(description) ? "Describe when VSCodex should use this skill." : description.Trim();
        return "---" + Environment.NewLine
            + "name: " + name + Environment.NewLine
            + "description: " + summary.Replace(Environment.NewLine, " ") + Environment.NewLine
            + "---" + Environment.NewLine
            + Environment.NewLine
            + "# " + name + Environment.NewLine
            + Environment.NewLine
            + summary + Environment.NewLine
            + Environment.NewLine
            + "## When To Use" + Environment.NewLine
            + "- Use this skill when the request matches the description above." + Environment.NewLine
            + Environment.NewLine
            + "## Workflow" + Environment.NewLine
            + "1. Inspect the local context needed for the request." + Environment.NewLine
            + "2. Apply the project conventions already in use." + Environment.NewLine
            + "3. Verify the result with the narrowest meaningful checks." + Environment.NewLine;
    }
}
