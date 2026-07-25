// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace VSCodex.Models;

/// <summary>Specifies the available orchestration Section Status values.</summary>
public enum OrchestrationSectionStatus
{
    /// <summary>Specifies the pending option.</summary>
    Pending,
    /// <summary>Specifies the running option.</summary>
    Running,
    /// <summary>Specifies the completed option.</summary>
    Completed,
    /// <summary>Specifies the failed option.</summary>
    Failed,
    /// <summary>Specifies the cancelled option.</summary>
    Cancelled
}
