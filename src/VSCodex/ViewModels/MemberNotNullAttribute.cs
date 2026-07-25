// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace System.Diagnostics.CodeAnalysis;

/// <summary>
/// Communicates postconditions for members initialized by helper methods on frameworks
/// that predate the nullable-analysis attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true)]
internal sealed class MemberNotNullAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="MemberNotNullAttribute"/> class.</summary>
    /// <param name="members">The members known to be non-null after the annotated member returns.</param>
    public MemberNotNullAttribute(params string[] members)
    {
        _ = members;
    }
}
