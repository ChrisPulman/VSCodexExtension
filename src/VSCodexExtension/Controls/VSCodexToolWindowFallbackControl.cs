using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.PlatformUI;

namespace VSCodexExtension.Controls
{
    internal sealed class VSCodexToolWindowFallbackControl : UserControl
    {
        public VSCodexToolWindowFallbackControl(Exception exception)
        {
            SetResourceReference(BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);

            var panel = new StackPanel { Margin = new Thickness(16), MaxWidth = 900 };
            panel.Children.Add(CreateText("VSCodex could not initialize", 18, FontWeights.SemiBold, 0, 0, 0, 8));
            panel.Children.Add(CreateText("The extension package loaded, but the ReactiveUI tool-window surface failed while it was being created. The details below are also written to the Visual Studio ActivityLog.", 12, FontWeights.Normal, 0, 0, 0, 12));
            panel.Children.Add(CreateText("Open VSCodex from View > Other Windows > VSCodex or Extensions > VSCodex after rebuilding the VSIX. If this fallback remains visible, the exception text identifies the startup component that failed.", 12, FontWeights.Normal, 0, 0, 0, 12));

            var exceptionText = new TextBox
            {
                Text = exception.ToString(),
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 220,
                Padding = new Thickness(8)
            };
            exceptionText.SetResourceReference(TextBox.BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);
            exceptionText.SetResourceReference(TextBox.ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
            exceptionText.SetResourceReference(TextBox.BorderBrushProperty, EnvironmentColors.ToolWindowBorderBrushKey);
            panel.Children.Add(exceptionText);

            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = panel
            };
        }

        private static TextBlock CreateText(string text, double size, FontWeight weight, double left, double top, double right, double bottom)
        {
            var block = new TextBlock
            {
                Text = text,
                FontSize = size,
                FontWeight = weight,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(left, top, right, bottom)
            };
            block.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
            return block;
        }
    }
}
