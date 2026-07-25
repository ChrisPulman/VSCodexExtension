// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.Windows.Data;

namespace VSCodex.Views;

/// <summary>Converts a container width into the width available after a fixed layout gutter.</summary>
public sealed class AvailableWidthConverter : IValueConverter
{
    /// <summary>Gets or sets the gutter removed from the supplied width.</summary>
    public double Gutter { get; set; }

    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = parameter;
        _ = culture;
        return value is double width ? Math.Max(0D, width - Gutter) : 0D;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
