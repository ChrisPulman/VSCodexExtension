// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.RegularExpressions;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Provides the skill Index Service implementation.</summary>
public sealed class SkillIndexService : ISkillIndexService
{
    /// <summary>Named number used by this type.</summary>
    private const int Numeric2 = 2;

    /// <summary>Matches YAML frontmatter.</summary>
    private static readonly Regex FrontmatterRegex = new("^---\\s*(.*?)\\s*---", RegexOptions.Singleline);

    /// <summary>Stores the skills.</summary>
    private readonly BehaviorSubject<IReadOnlyList<SkillDefinition>> _skills = new([]);

    /// <summary>Gets the skills.</summary>
    public IObservable<IReadOnlyList<SkillDefinition>> Skills => _skills.AsObservable();

    /// <summary>Gets the snapshot.</summary>
    public IReadOnlyList<SkillDefinition> Snapshot => _skills.Value;

    /// <summary>Refreshes the operation.</summary>
    /// <param name="roots">The roots.</param>
    public void Refresh(IEnumerable<string> roots)
    {
        var results = new List<SkillDefinition>();
        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(root, "SKILL.md", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(file);
                var skillRoot = Path.GetDirectoryName(file)!;
                results.Add(
                    new SkillDefinition
                    {
                        Name = ReadFrontmatter(content, "name") ?? new DirectoryInfo(skillRoot).Name,
                        Description = ReadFrontmatter(content, "description") ?? FirstParagraph(content),
                        RootPath = skillRoot,
                        MarkdownPath = file,
                        Content = content,
                    });
            }
        }

        _skills.OnNext(results.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList());
    }

    /// <summary>Creates skill.</summary>
    /// <param name="root">The root.</param>
    /// <param name="name">The name.</param>
    /// <param name="description">The description.</param>
    /// <returns>The create Skill result.</returns>
    public string CreateSkill(string root, string name, string description)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new ArgumentException("A skill root folder is required.", nameof(root));
        }

        var normalizedName = NormalizeSkillName(name);
        var skillRoot = Path.Combine(root, normalizedName);
        _ = Directory.CreateDirectory(skillRoot);
        var skillPath = Path.Combine(skillRoot, "SKILL.md");
        if (!File.Exists(skillPath))
        {
            File.WriteAllText(skillPath, BuildSkillTemplate(normalizedName, description), new UTF8Encoding(false));
        }

        return skillPath;
    }

    /// <summary>Reads frontmatter.</summary>
    /// <param name="content">The content.</param>
    /// <param name="key">The key.</param>
    /// <returns>The read Frontmatter result.</returns>
    private static string? ReadFrontmatter(string content, string key)
    {
        var match = FrontmatterRegex.Match(content);
        if (!match.Success)
        {
            return null;
        }

        foreach (var line in match.Groups[1].Value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split([':'], Numeric2);
            if (parts.Length == Numeric2 && parts[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return parts[1].Trim().Trim('"');
            }
        }

        return null;
    }

    /// <summary>Performs the first Paragraph operation.</summary>
    /// <param name="content">The content.</param>
    /// <returns>The first Paragraph result.</returns>
    private static string FirstParagraph(string content) => content
        .Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries)
        .Skip(1)
        .FirstOrDefault()?
        .Trim() ?? string.Empty;

    /// <summary>Performs the normalize Skill Name operation.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The normalize Skill Name result.</returns>
    private static string NormalizeSkillName(string name)
    {
        var value = (name ?? string.Empty).Trim();
        if (value.Length == 0 || !char.IsLetterOrDigit(value[0]) || value.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '-')))
        {
            throw new ArgumentException("Skill names must start with a letter or digit and can contain letters, digits, '.', '_' or '-'.", nameof(name));
        }

        return value;
    }

    /// <summary>Builds skill Template.</summary>
    /// <param name="name">The name.</param>
    /// <param name="description">The description.</param>
    /// <returns>The build Skill Template result.</returns>
    private static string BuildSkillTemplate(string name, string description)
    {
        var summary = string.IsNullOrWhiteSpace(description)
            ? "Describe when VSCodex should use this skill."
            : description.Trim();
        return string.Join(
            Environment.NewLine,
            [
                "---",
                $"name: {name}",
                $"description: {summary.Replace(Environment.NewLine, " ")}",
                "---",
                string.Empty,
                $"# {name}",
                string.Empty,
                summary,
                string.Empty,
                "## When To Use",
                "- Use this skill when the request matches the description above.",
                string.Empty,
                "## Workflow",
                "1. Inspect the local context needed for the request.",
                "2. Apply the project conventions already in use.",
                "3. Verify the result with the narrowest meaningful checks.",
                string.Empty,
            ]);
    }
}
