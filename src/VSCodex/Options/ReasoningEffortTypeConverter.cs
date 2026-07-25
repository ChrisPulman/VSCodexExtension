// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.ComponentModel;
using System.Linq;

namespace VSCodex.Options;

/// <summary>Provides the reasoning Effort Type Converter implementation.</summary>
public sealed class ReasoningEffortTypeConverter : StringConverter
{
    /// <summary>Gets standard Values Supported.</summary>
    /// <param name="context">The context.</param>
    /// <returns><see langword="true"/> when get Standard Values Supported succeeds; otherwise, <see langword="false"/>.</returns>
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    /// <summary>Gets standard Values Exclusive.</summary>
    /// <param name="context">The context.</param>
    /// <returns><see langword="true"/> when get Standard Values Exclusive succeeds; otherwise, <see langword="false"/>.</returns>
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;

    /// <summary>Gets standard Values.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The get Standard Values result.</returns>
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
        => new(OptionsStandardValues.GetReasoningEfforts(context).ToArray());
}
