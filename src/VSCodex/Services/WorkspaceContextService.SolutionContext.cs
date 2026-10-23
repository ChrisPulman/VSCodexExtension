// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using VSCodex.Core.Models;

namespace VSCodex.Services;

/// <summary>Captures live Visual Studio metadata without source bodies.</summary>
public sealed partial class WorkspaceContextService
{
    /// <summary>Reads bounded metadata on the UI thread.</summary>
    /// <returns>An immutable metadata snapshot.</returns>
    public SolutionContextSnapshot CaptureSolutionContext()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return new MetadataCapture().Capture(_serviceProvider);
    }

    /// <summary>Contains mutable capture state only for the duration of one UI-thread read.</summary>
    private sealed class MetadataCapture
    {
        /// <summary>Bounds solution folder nesting.</summary>
        private const int MaxFolderDepth = 16;

        /// <summary>Bounds enumeration of each solution folder.</summary>
        private const int MaxFolderItems = 256;

        /// <summary>Stores bounded capture state.</summary>
        private readonly List<string> _diagnostics = new();

        /// <summary>Stores bounded capture state.</summary>
        private readonly List<string> _projects = new();

        /// <summary>Stores bounded capture state.</summary>
        private readonly List<string> _documents = new();

        /// <summary>Stores bounded capture state.</summary>
        private readonly List<string> _startup = new();

        /// <summary>Stores bounded capture state.</summary>
        private readonly HashSet<string> _visited = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Reads independent metadata from the current automation model.</summary>
        /// <param name="provider">The Visual Studio service provider.</param>
        /// <returns>The immutable captured metadata.</returns>
        public SolutionContextSnapshot Capture(IServiceProvider provider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = Read(
                () =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return provider.GetService(typeof(DTE)) as DTE;
                },
                null,
                "Visual Studio");
            var solution = Read(() => dte?.Solution, null, "Solution");
            var build = Read(() => solution?.SolutionBuild, null, "Solution build");
            var configuration = Read(() => build?.ActiveConfiguration, null, "Active configuration");
            var configurationName = Read(() => configuration?.Name ?? string.Empty, string.Empty, "Configuration name");
            var platform = ReadPlatform(configuration);
            ReadStartup(build);
            ReadProjects(solution);
            var activeDocument = ReadDocuments(dte);
            return new(Read(() => solution?.FullName ?? string.Empty, string.Empty, "Solution path"), (configurationName, platform), _startup, _projects, activeDocument, _documents, _diagnostics);
        }

        /// <summary>Reads the active platform.</summary>
        /// <param name="configuration">The active solution configuration.</param>
        /// <returns>The platform name if available.</returns>
        private string ReadPlatform(SolutionConfiguration? configuration)
        {
            var platform = string.Empty;
            var contexts = Read(() => configuration?.SolutionContexts, null, "Solution contexts");
            if (contexts is not null && Read(
                () =>
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                return contexts.Count;
            },
                0,
                "Solution context count") > 0)
            {
                platform = Read(
                    () =>
                {
                    Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                    return contexts.Item(1).PlatformName;
                },
                    string.Empty,
                    "Platform");
            }

            return platform;
        }

        /// <summary>Reads _startup project names.</summary>
        /// <param name="build">The solution build.</param>
        private void ReadStartup(SolutionBuild? build)
        {
            if (Read(() => build?.StartupProjects, null, "Startup _projects") is not Array startupArray)
            {
                return;
            }

            foreach (var item in startupArray)
            {
                if (_startup.Count >= SolutionContextSnapshot.MaxProjects)
                {
                    break;
                }

                _startup.Add(item?.ToString() ?? string.Empty);
            }
        }

        /// <summary>Reads the bounded project tree.</summary>
        /// <param name="solution">The active solution.</param>
        private void ReadProjects(Solution? solution)
        {
            var roots = Read(() => solution?.Projects, null, "Projects");
            var rootCount = Read(() => roots?.Count ?? 0, 0, "Project count");
            for (var i = 1; i <= rootCount && i <= SolutionContextSnapshot.MaxProjects; i++)
            {
                var index = i;
                var project = Read(
                    () =>
                {
                    Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                    return roots?.Item(index);
                },
                    null,
                    "Project entry");
                if (project is not null)
                {
                    Visit(project, 0);
                }
            }

            if (rootCount <= SolutionContextSnapshot.MaxProjects || _diagnostics.Count >= SolutionContextSnapshot.MaxDocuments)
            {
                return;
            }

            _diagnostics.Add("Project list truncated.");
        }

        /// <summary>Reads document paths and dirty state without text buffers.</summary>
        /// <param name="dte">The automation model.</param>
        /// <returns>The active document path and dirty flag.</returns>
        private (string Path, bool? Dirty) ReadDocuments(DTE? dte)
        {
            var active = Read(() => dte?.ActiveDocument, null, "Active document");
            var activePath = Read(() => active?.FullName ?? string.Empty, string.Empty, "Active document path");
            bool? dirty = active is null ? null : Read<bool?>(
                () =>
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                return !active.Saved;
            },
                null,
                "Active document state");
            var open = Read(() => dte?.Documents, null, "Open documents");
            var documentCount = Read(() => open?.Count ?? 0, 0, "Open document count");
            for (var i = 1; i <= documentCount && i <= SolutionContextSnapshot.MaxDocuments; i++)
            {
                var index = i;
                var path = Read(
                    () =>
                    {
                        ThreadHelper.ThrowIfNotOnUIThread();
                        return open?.Item(index)?.FullName ?? string.Empty;
                    },
                    "[unavailable]",
                    "Open document path");
                _documents.Add(path);
            }

            if (documentCount > SolutionContextSnapshot.MaxDocuments && _diagnostics.Count < SolutionContextSnapshot.MaxDocuments)
            {
                _diagnostics.Add("Open document list truncated.");
            }

            return (activePath, dirty);
        }

        /// <summary>Reads metadata for one project.</summary>
        /// <param name="project">The project.</param>
        /// <param name="depth">The folder depth.</param>
        private void Visit(Project project, int depth)
        {
            if (_projects.Count >= SolutionContextSnapshot.MaxProjects || depth > MaxFolderDepth)
            {
                if (_diagnostics.Count < SolutionContextSnapshot.MaxDocuments)
                {
                    _diagnostics.Add("Project traversal truncated at metadata limit.");
                }

                return;
            }

            var name = Read(
                () =>
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                return project.Name;
            },
                "[unavailable]",
                "Project name");
            var path = Read(
                () =>
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                return project.FullName;
            },
                string.Empty,
                $"Project path: {name}");
            var kind = Read(
                () =>
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                return project.Kind;
            },
                string.Empty,
                $"Project kind: {name}");
            var key = Read(
                () =>
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                return project.UniqueName;
            },
                $"{name}|{path}",
                $"Project identity: {name}");
            if (!_visited.Add(key))
            {
                return;
            }

            _projects.Add($"{name} | {path} | {kind}");

            // Solution folders expose nested _projects through ProjectItems; C++ and unloaded _projects may reject these properties.
            if (!string.Equals(kind, "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ReadChildren(project, name, depth);
        }

        /// <summary>Reads child _projects in a solution folder.</summary>
        /// <param name="project">The folder project.</param>
        /// <param name="name">The folder name.</param>
        /// <param name="depth">The current folder depth.</param>
        private void ReadChildren(Project project, string name, int depth)
        {
            var items = Read(
                () =>
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                return project.ProjectItems;
            },
                null,
                $"Nested _projects: {name}");
            var count = Read(() => items?.Count ?? 0, 0, "Nested project count");
            for (var i = 1; i <= count && i <= MaxFolderItems && _projects.Count < SolutionContextSnapshot.MaxProjects; i++)
            {
                var index = i;
                var child = Read(
                    () =>
                {
                    Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                    return items?.Item(index)?.SubProject;
                },
                    null,
                    "Nested project");
                if (child is not null)
                {
                    Visit(child, depth + 1);
                }
            }

            if (count <= MaxFolderItems || _diagnostics.Count >= SolutionContextSnapshot.MaxDocuments)
            {
                return;
            }

            _diagnostics.Add("Nested project enumeration truncated.");
        }

        /// <summary>Preserves available metadata when one automation property fails.</summary>
        /// <typeparam name="T">The property type.</typeparam>
        /// <param name="read">The property reader.</param>
        /// <param name="fallback">The unavailable value.</param>
        /// <param name="area">The diagnostic area.</param>
        /// <returns>The captured value or its fallback.</returns>
        private T Read<T>(Func<T> read, T fallback, string area)
        {
            try
            {
                return read();
            }
            catch (Exception exception)
            {
                if (_diagnostics.Count < SolutionContextSnapshot.MaxDocuments)
                {
                    _diagnostics.Add($"{area} unavailable: {exception.GetType().Name}");
                }

                return fallback;
            }
        }
    }
}
