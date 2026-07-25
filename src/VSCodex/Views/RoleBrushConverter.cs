// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using VSCodex.Models;

namespace VSCodex.Views;

/// <summary>Provides the role Brush Converter implementation.</summary>
public sealed class RoleBrushConverter : IValueConverter
{
    /// <summary>Converts the operation.</summary>
    /// <param name="value">The value.</param>
    /// <param name="targetType">The target Type.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The convert result.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        ThemeResourceKey key = (ThemeResourceKey)((value is not CodexMessageRole) ? EnvironmentColors.ToolWindowTextBrushKey : ((CodexMessageRole)value switch
        {
            CodexMessageRole.User => EnvironmentColors.FileTabHotTextBrushKey,
            CodexMessageRole.Error => EnvironmentColors.ToolWindowValidationErrorTextBrushKey,
            CodexMessageRole.Memory => EnvironmentColors.SystemGrayTextBrushKey,
            CodexMessageRole.Skill or CodexMessageRole.Mcp => EnvironmentColors.CommandBarMenuLinkTextBrushKey,
            _ => EnvironmentColors.ToolWindowTextBrushKey,
        }));
        return (Application.Current?.TryFindResource(key) as Brush) ?? SystemColors.ControlTextBrush;
    }

    /// <summary>Converts back.</summary>
    /// <param name="value">The value.</param>
    /// <param name="targetType">The target Type.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The convert Back result.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
