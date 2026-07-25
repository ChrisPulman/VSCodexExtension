// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Generic;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Defines the i Model Analytics Service contract.</summary>
public interface IModelAnalyticsService
{
    /// <summary>Gets the profiles.</summary>
    IReadOnlyList<ModelProfile> Profiles { get; }

    /// <summary>Estimates the operation.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The estimate result.</returns>
    ModelUsageEstimate Estimate(CodexRunRequest request);
}
