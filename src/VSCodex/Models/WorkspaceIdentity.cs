// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace VSCodex.Models;

/// <summary>Provides the workspace Identity implementation.</summary>
public sealed class WorkspaceIdentity
{
    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the root Path.</summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the solution Path.</summary>
    public string SolutionPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the solution Relative Path.</summary>
    public string SolutionRelativePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the repository Remote.</summary>
    public string RepositoryRemote { get; set; } = string.Empty;

    /// <summary>Gets or sets the memory Root.</summary>
    public string MemoryRoot { get; set; } = string.Empty;
}
