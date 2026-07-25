// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace VSCodex.Models;

/// <summary>Specifies the available sandbox Mode values.</summary>
public enum SandboxMode
{
    /// <summary>Specifies the read Only option.</summary>
    ReadOnly,
    /// <summary>Specifies the workspace Write option.</summary>
    WorkspaceWrite,
    /// <summary>Specifies the danger Full Access option.</summary>
    DangerFullAccess
}
