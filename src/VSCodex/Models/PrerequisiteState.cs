// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace VSCodex.Models;

/// <summary>Specifies the available prerequisite State values.</summary>
public enum PrerequisiteState
{
    /// <summary>Specifies the ready option.</summary>
    Ready,
    /// <summary>Specifies the warning option.</summary>
    Warning,
    /// <summary>Specifies the missing option.</summary>
    Missing,
    /// <summary>Specifies the error option.</summary>
    Error
}
