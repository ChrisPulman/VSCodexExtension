// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSCodex.Core.Models;

/// <summary>Immutable, bounded metadata captured from Visual Studio without document bodies.</summary>
public sealed class SolutionContextSnapshot
{
    /// <summary>Bounds the number of projects.</summary>
    public static readonly int MaxProjects = MaxProjectsLimit;

    /// <summary>Bounds the number of documents.</summary>
    public static readonly int MaxDocuments = MaxDocumentsLimit;

    /// <summary>Bounds individual metadata fields.</summary>
    public static readonly int MaxTextLength = MaxTextLengthLimit;

    /// <summary>Bounds formatted metadata output.</summary>
    public static readonly int MaxFormattedLength = MaxFormattedLengthLimit;

    /// <summary>Bounds captured metadata.</summary>
    private const int MaxProjectsLimit = 64;

    /// <summary>Bounds captured metadata.</summary>
    private const int MaxDocumentsLimit = 16;

    /// <summary>Bounds captured metadata.</summary>
    private const int MaxTextLengthLimit = 512;

    /// <summary>Bounds captured metadata.</summary>
    private const int MaxFormattedLengthLimit = 16_000;

    /// <summary>Marks omitted formatted metadata.</summary>
    private const string TruncationMarker = "\n[Metadata output truncated]\n";

    /// <summary>Marks abbreviated metadata fields.</summary>
    private const string Ellipsis = "...";

    /// <summary>Initializes a new instance of the <see cref="SolutionContextSnapshot"/> class.</summary>
    /// <param name="solutionPath">Captured solutionPath metadata.</param>
    /// <param name="configuration">Captured configuration metadata.</param>
    /// <param name="startupProjects">Captured startupProjects metadata.</param>
    /// <param name="projects">Captured projects metadata.</param>
    /// <param name="activeDocument">Captured activeDocument metadata.</param>
    /// <param name="openDocuments">Captured openDocuments metadata.</param>
    /// <param name="diagnostics">Captured diagnostics metadata.</param>
    public SolutionContextSnapshot(
        string solutionPath,
        (string Name, string Platform) configuration,
        IEnumerable<string> startupProjects,
        IEnumerable<string> projects,
        (string Path, bool? Dirty) activeDocument,
        IEnumerable<string> openDocuments,
        IEnumerable<string> diagnostics)
    {
        SolutionPath = Bound(solutionPath);
        Configuration = Bound(configuration.Name);
        Platform = Bound(configuration.Platform);
        StartupProjects = Copy(startupProjects, MaxProjects);
        Projects = Copy(projects, MaxProjects);
        ActiveDocument = Bound(activeDocument.Path);
        ActiveDocumentDirty = activeDocument.Dirty;
        OpenDocuments = Copy(openDocuments, MaxDocuments);
        Diagnostics = Copy(diagnostics, MaxDocuments);
    }

    /// <summary>Gets the } metadata value.</summary>
    public string SolutionPath { get; }

    /// <summary>Gets the } metadata value.</summary>
    public string Configuration { get; }

    /// <summary>Gets the } metadata value.</summary>
    public string Platform { get; }

    /// <summary>Gets the } metadata value.</summary>
    public IReadOnlyList<string> StartupProjects { get; }

    /// <summary>Gets the } metadata value.</summary>
    public IReadOnlyList<string> Projects { get; }

    /// <summary>Gets the } metadata value.</summary>
    public string ActiveDocument { get; }

    /// <summary>Gets the } metadata value.</summary>
    public bool? ActiveDocumentDirty { get; }

    /// <summary>Gets the } metadata value.</summary>
    public IReadOnlyList<string> OpenDocuments { get; }

    /// <summary>Gets the } metadata value.</summary>
    public IReadOnlyList<string> Diagnostics { get; }

    /// <summary>Formats metadata as bounded contextual data, with no source content.</summary>
    /// <returns>The bounded metadata section.</returns>
    public string Format()
    {
        var text = new StringBuilder("## Visual Studio metadata snapshot\nMetadata only; captured when this prompt was queued.\n");
        _ = text.AppendLine($"Solution: {SolutionPath}")
        .AppendLine($"Configuration: {Configuration}; Platform: {Platform}")
        .AppendLine($"Startup projects: {string.Join(", ", StartupProjects)}")
        .AppendLine($"Active document: {ActiveDocument}; Dirty: {ActiveDocumentDirty?.ToString() ?? "unknown"}");
        Append(text, "Projects (name | path | kind):", Projects);
        Append(text, "Open documents:", OpenDocuments);
        Append(text, "Capture diagnostics:", Diagnostics);
        var result = text.ToString();
        return result.Length <= MaxFormattedLength ? result : $"{result.Substring(0, MaxFormattedLength - TruncationMarker.Length)}{TruncationMarker}";
    }

    /// <summary>Bounds and formats captured metadata.</summary>
    /// <param name="values">The captured values.</param>
    /// <param name="limit">The list bound.</param>
    /// <returns>An independent read-only list.</returns>
    private static IReadOnlyList<string> Copy(IEnumerable<string> values, int limit) => Array.AsReadOnly(values.Take(limit).Select(Bound).ToArray());

    /// <summary>Bounds and formats captured metadata.</summary>
    /// <param name="value">The captured value.</param>
    /// <returns>A single bounded line.</returns>
    private static string Bound(string value)
    {
        value = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
        return value.Length <= MaxTextLength ? value : $"{value.Substring(0, MaxTextLength - Ellipsis.Length)}{Ellipsis}";
    }

    /// <summary>Bounds and formats captured metadata.</summary>
    /// <param name="text">The output.</param>
    /// <param name="title">The section title.</param>
    /// <param name="values">The captured values.</param>
    private static void Append(StringBuilder text, string title, IEnumerable<string> values)
    {
        _ = text.AppendLine(title);
        foreach (var value in values)
        {
            _ = text.AppendLine($"- {value}");
        }
    }
}
