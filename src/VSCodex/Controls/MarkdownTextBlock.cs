// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace VSCodex.Controls;

/// <summary>Provides the markdown Text Block implementation.</summary>
public sealed class MarkdownTextBlock : TextBlock
{
    /// <summary>Stores the markdown Property.</summary>
    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.Register(nameof(Markdown), typeof(string), typeof(MarkdownTextBlock), new(string.Empty, OnMarkdownChanged));

    /// <summary>Named number used by this type.</summary>
    private const double Numeric0Point96 = 0.96;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric2 = 2;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric3 = 3;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric4 = 4;

    /// <summary>Specifies the bold inline token kind.</summary>
    private const int InlineKindBold = 3;

    /// <summary>Specifies the code inline token kind.</summary>
    private const int InlineKindCode = 2;

    /// <summary>Specifies the link inline token kind.</summary>
    private const int InlineKindLink = 1;

    /// <summary>Specifies the absence of an inline token kind.</summary>
    private const int InlineKindNone = 0;

    /// <summary>Stores the link Pattern.</summary>
    private static readonly Regex LinkPattern = new(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);

    /// <summary>Stores the bullet Pattern.</summary>
    private static readonly Regex BulletPattern = new(@"^\s*[-*]\s+", RegexOptions.Compiled);

    /// <summary>Stores the numbered Pattern.</summary>
    private static readonly Regex NumberedPattern = new(@"^\s*(\d+\.)\s+", RegexOptions.Compiled);

    /// <summary>Stores the line suffix Pattern.</summary>
    private static readonly Regex LineSuffixPattern = new(@"^(?<path>.+):(?<line>\d+)$", RegexOptions.Compiled);

    /// <summary>Gets or sets the markdown.</summary>
    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>Handles the markdown Changed event.</summary>
    /// <param name="d">The d.</param>
    /// <param name="e">The e.</param>
    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MarkdownTextBlock)d).Render((string?)e.NewValue ?? string.Empty);
    }

    /// <summary>Finds next Inline Token.</summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The start.</param>
    /// <returns>The find Next Inline Token result.</returns>
    private static InlineToken FindNextInlineToken(string text, int start)
    {
        var best = new InlineToken(-1, InlineKindNone);
        var link = LinkPattern.Match(text, start);
        if (link.Success)
        {
            best = new(link.Index, InlineKindLink);
        }

        var code = text.IndexOf('`', start);
        if (code >= 0 && text.IndexOf('`', code + 1) > code && (best.Index < 0 || code < best.Index))
        {
            best = new(code, InlineKindCode);
        }

        var bold = text.IndexOf("**", start, StringComparison.Ordinal);
        if (bold >= 0 && text.IndexOf("**", bold + Numeric2, StringComparison.Ordinal) > bold && (best.Index < 0 || bold < best.Index))
        {
            best = new(bold, InlineKindBold);
        }

        return best;
    }

    /// <summary>Handles the hyperlink Click event.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private static void OnHyperlinkClick(object sender, RoutedEventArgs e)
    {
        if (!(sender is Hyperlink link) || !(link.Tag is string target) || string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        try
        {
            var path = StripLineSuffix(target);
            var targetToOpen = File.Exists(path) || Directory.Exists(path) ? path : target;
            _ = Process.Start(new ProcessStartInfo(targetToOpen) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception, nameof(OnHyperlinkClick));
        }
    }

    /// <summary>Performs the strip Line Suffix operation.</summary>
    /// <param name="target">The target.</param>
    /// <returns>The strip Line Suffix result.</returns>
    private static string StripLineSuffix(string target)
    {
        var match = LineSuffixPattern.Match(target);
        return match.Success && File.Exists(match.Groups["path"].Value) ? match.Groups["path"].Value : target;
    }

    /// <summary>Renders the operation.</summary>
    /// <param name="markdown">The markdown.</param>
    private void Render(string markdown)
    {
        Inlines.Clear();
        var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var inCodeBlock = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                inCodeBlock = !inCodeBlock;
                if (i < lines.Length - 1)
                {
                    Inlines.Add(new LineBreak());
                }

                continue;
            }

            if (inCodeBlock)
            {
                var codeRun = new Run(line)
                {
                    FontFamily = new("Consolas")
                };
                codeRun.FontSize = FontSize;
                codeRun.FontSize *= Numeric0Point96;
                Inlines.Add(codeRun);
            }
            else if (trimmed.Length == 0)
            {
                Inlines.Add(new LineBreak());
            }
            else
            {
                RenderMarkdownLine(line);
            }

            if (i < lines.Length - 1)
            {
                Inlines.Add(new LineBreak());
            }
        }
    }

    /// <summary>Renders markdown Line.</summary>
    /// <param name="line">The line.</param>
    private void RenderMarkdownLine(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("# ", StringComparison.Ordinal))
        {
            AddInlineRuns(trimmed.Substring(Numeric2), FontWeights.SemiBold, FontStyles.Normal, FontSize + Numeric2);
            return;
        }

        if (trimmed.StartsWith("## ", StringComparison.Ordinal))
        {
            AddInlineRuns(trimmed.Substring(Numeric3), FontWeights.SemiBold, FontStyles.Normal, FontSize + 1);
            return;
        }

        if (trimmed.StartsWith("**", StringComparison.Ordinal) && trimmed.EndsWith("**", StringComparison.Ordinal) && trimmed.Length > Numeric4)
        {
            AddInlineRuns(trimmed.Substring(Numeric2, trimmed.Length - Numeric4), FontWeights.SemiBold, FontStyles.Normal, FontSize);
            return;
        }

        var bullet = BulletPattern.Match(line);
        if (bullet.Success)
        {
            Inlines.Add(new Run("\u2022 ") { FontWeight = FontWeights.SemiBold });
            AddInlineRuns(line.Substring(bullet.Length), FontWeights.Normal, FontStyles.Normal, FontSize);
            return;
        }

        var number = NumberedPattern.Match(line);
        if (number.Success)
        {
            Inlines.Add(new Run($"{number.Groups[1].Value} ") { FontWeight = FontWeights.SemiBold });
            AddInlineRuns(line.Substring(number.Length), FontWeights.Normal, FontStyles.Normal, FontSize);
            return;
        }

        AddInlineRuns(line.TrimStart(), FontWeights.Normal, FontStyles.Normal, FontSize);
    }

    /// <summary>Adds inline Runs.</summary>
    /// <param name="text">The text.</param>
    /// <param name="weight">The weight.</param>
    /// <param name="style">The style.</param>
    /// <param name="size">The size.</param>
    private void AddInlineRuns(string text, FontWeight weight, FontStyle style, double size)
    {
        var index = 0;
        while (index < text.Length)
        {
            var next = FindNextInlineToken(text, index);
            if (next.Index < 0)
            {
                AddRun(text.Remove(0, index), weight, style, size, false);
                break;
            }

            if (next.Index > index)
            {
                AddRun(text.Substring(index, next.Index - index), weight, style, size, false);
            }

            if (next.Kind == InlineKindLink)
            {
                var match = LinkPattern.Match(text, next.Index);
                AddHyperlink(match.Groups[1].Value, match.Groups[Numeric2].Value.Trim());
                index = match.Index + match.Length;
            }
            else if (next.Kind == InlineKindCode)
            {
                var end = text.IndexOf('`', next.Index + 1);
                AddRun(text.Substring(next.Index + 1, end - next.Index - 1), FontWeights.Normal, FontStyles.Normal, size, true);
                index = end + 1;
            }
            else
            {
                var end = text.IndexOf("**", next.Index + Numeric2, StringComparison.Ordinal);
                AddInlineRuns(text.Substring(next.Index + Numeric2, end - next.Index - Numeric2), FontWeights.SemiBold, style, size);
                index = end + Numeric2;
            }
        }
    }

    /// <summary>Adds run.</summary>
    /// <param name="text">The text.</param>
    /// <param name="weight">The weight.</param>
    /// <param name="style">The style.</param>
    /// <param name="size">The size.</param>
    /// <param name="code">The code.</param>
    private void AddRun(string text, FontWeight weight, FontStyle style, double size, bool code)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Inlines.Add(new Run(text)
        {
            FontFamily = code ? new FontFamily("Consolas") : FontFamily,
            FontSize = code ? size * Numeric0Point96 : size,
            FontStyle = style,
            FontWeight = weight
        });
    }

    /// <summary>Adds hyperlink.</summary>
    /// <param name="label">The label.</param>
    /// <param name="target">The target.</param>
    private void AddHyperlink(string label, string target)
    {
        var link = new Hyperlink(new Run(label))
        {
            Cursor = Cursors.Hand,
            Tag = target.Trim('<', '>')
        };
        link.Click += OnHyperlinkClick;
        Inlines.Add(link);
    }

    /// <summary>Provides the inline Token implementation.</summary>
    private readonly struct InlineToken
    {
        /// <summary>Initializes a new instance of the <see cref="InlineToken"/> class.</summary>
        /// <param name="index">The index.</param>
        /// <param name="kind">The kind.</param>
        public InlineToken(int index, int kind)
        {
            Index = index;
            Kind = kind;
        }

        /// <summary>Gets the index.</summary>
        public int Index { get; }

        /// <summary>Gets the kind.</summary>
        public int Kind { get; }
    }
}
