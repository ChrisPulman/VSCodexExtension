// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace VSCodex.Models;

/// <summary>Specifies the available run Activity Kind values.</summary>
public enum RunActivityKind
{
    /// <summary>Specifies the user option.</summary>
    User,
    /// <summary>Specifies the agent option.</summary>
    Agent,
    /// <summary>Specifies the mcp option.</summary>
    Mcp,
    /// <summary>Specifies the skill option.</summary>
    Skill,
    /// <summary>Specifies the files option.</summary>
    Files,
    /// <summary>Specifies the assistant option.</summary>
    Assistant,
    /// <summary>Specifies the system option.</summary>
    System,
    /// <summary>Specifies the file option.</summary>
    File
}
