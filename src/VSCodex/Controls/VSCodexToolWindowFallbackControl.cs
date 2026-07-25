// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.PlatformUI;

namespace VSCodex.Controls;

/// <summary>Provides the vS Codex Tool Window Fallback Control implementation.</summary>
internal sealed class VSCodexToolWindowFallbackControl : UserControl
{
    /// <summary>Named number used by this type.</summary>
    private const int Numeric12 = 12;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric16 = 16;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric18 = 18;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric220 = 220;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric8 = 8;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric900 = 900;

    /// <summary>Initializes a new instance of the <see cref="VSCodexToolWindowFallbackControl"/> class.</summary>
    /// <param name="exception">The exception.</param>
    public VSCodexToolWindowFallbackControl(Exception exception)
    {
        SetResourceReference(BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);

        var panel = new StackPanel { Margin = new(Numeric16), MaxWidth = Numeric900 };
        _ = panel.Children.Add(CreateText("VSCodex could not initialize", Numeric18, FontWeights.SemiBold, 0, 0, 0, Numeric8));
        _ = panel.Children.Add(CreateText(
            "The extension package loaded, but the ReactiveUI tool-window surface failed while it was being created. " +
            "The details below are also written to the Visual Studio ActivityLog.",
            Numeric12,
            FontWeights.Normal,
            0,
            0,
            0,
            Numeric12));
        _ = panel.Children.Add(CreateText(
            "Open VSCodex from View > Other Windows > VSCodex or Extensions > VSCodex after rebuilding the VSIX. " +
            "If this fallback remains visible, the exception text identifies the startup component that failed.",
            Numeric12,
            FontWeights.Normal,
            0,
            0,
            0,
            Numeric12));

        var exceptionText = new TextBox
        {
            Text = exception.ToString(),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = Numeric220,
            Padding = new(Numeric8)
        };
        exceptionText.SetResourceReference(TextBox.BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);
        exceptionText.SetResourceReference(TextBox.ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
        exceptionText.SetResourceReference(TextBox.BorderBrushProperty, EnvironmentColors.ToolWindowBorderBrushKey);
        _ = panel.Children.Add(exceptionText);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = panel
        };
    }

    /// <summary>Creates text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="size">The size.</param>
    /// <param name="weight">The weight.</param>
    /// <param name="left">The left.</param>
    /// <param name="top">The top.</param>
    /// <param name="right">The right.</param>
    /// <param name="bottom">The bottom.</param>
    /// <returns>The create Text result.</returns>
    private static TextBlock CreateText(string text, double size, FontWeight weight, double left, double top, double right, double bottom)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
            Margin = new(left, top, right, bottom)
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
        return block;
    }
}
