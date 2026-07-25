// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace VSCodex.Models;

/// <summary>Specifies the available approval Policy values.</summary>
public enum ApprovalPolicy
{
    /// <summary>Specifies the untrusted option.</summary>
    Untrusted,
    /// <summary>Specifies the on Failure option.</summary>
    OnFailure,
    /// <summary>Specifies the on Request option.</summary>
    OnRequest,
    /// <summary>Specifies the never option.</summary>
    Never
}
