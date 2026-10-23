// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.Windows.Data;

namespace VSCodex.Views;

/// <summary>Reserves conversation space while keeping a tall composer scrollable.</summary>
public sealed class ComposerHeightConverter : IValueConverter
{
    /// <summary>Fraction of the available tool-window height reserved for the composer.</summary>
    private const double ComposerFraction = 0.45D;

    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = parameter;
        _ = culture;
        return value is double height && !double.IsNaN(height) ? Math.Max(0D, height * ComposerFraction) : 0D;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
