// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace VSCodex.Models;

/// <summary>Specifies the available codex Message Role values.</summary>
public enum CodexMessageRole
{
    /// <summary>Specifies the system option.</summary>
    System,
    /// <summary>Specifies the user option.</summary>
    User,
    /// <summary>Specifies the assistant option.</summary>
    Assistant,
    /// <summary>Specifies the tool option.</summary>
    Tool,
    /// <summary>Specifies the approval option.</summary>
    Approval,
    /// <summary>Specifies the error option.</summary>
    Error,
    /// <summary>Specifies the memory option.</summary>
    Memory,
    /// <summary>Specifies the skill option.</summary>
    Skill,
    /// <summary>Specifies the mcp option.</summary>
    Mcp
}
