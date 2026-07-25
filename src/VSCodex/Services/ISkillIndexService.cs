// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Defines the skill index service contract.</summary>
public interface ISkillIndexService
{
    /// <summary>Gets the skills.</summary>
    IObservable<IReadOnlyList<SkillDefinition>> Skills { get; }

    /// <summary>Gets the snapshot.</summary>
    IReadOnlyList<SkillDefinition> Snapshot { get; }

    /// <summary>Refreshes the operation.</summary>
    /// <param name="roots">The roots.</param>
    void Refresh(IEnumerable<string> roots);

    /// <summary>Creates a skill.</summary>
    /// <param name="root">The root.</param>
    /// <param name="name">The name.</param>
    /// <param name="description">The description.</param>
    /// <returns>The created skill path.</returns>
    string CreateSkill(string root, string name, string description);
}
