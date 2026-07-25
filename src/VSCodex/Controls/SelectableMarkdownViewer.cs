// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace VSCodex.Controls;

/// <summary>Provides the selectable Markdown Viewer implementation.</summary>
public sealed class SelectableMarkdownViewer : RichTextBox
{
    /// <summary>Stores the markdown Property.</summary>
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(nameof(Markdown), typeof(string), typeof(SelectableMarkdownViewer), new(string.Empty, OnMarkdownChanged));

    /// <summary>Named number used by this type.</summary>
    private const double Numeric0Point96 = 0.96;

    /// <summary>Named number used by this type.</summary>
    private const double Numeric18Point0 = 18.0;

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
    private static readonly Regex LinkPattern = new("\\[([^\\]]+)\\]\\(([^)]+)\\)", RegexOptions.Compiled);

    /// <summary>Stores the bullet Pattern.</summary>
    private static readonly Regex BulletPattern = new("^\\s*[-*]\\s+", RegexOptions.Compiled);

    /// <summary>Stores the numbered Pattern.</summary>
    private static readonly Regex NumberedPattern = new("^\\s*(\\d+\\.)\\s+", RegexOptions.Compiled);

    /// <summary>Stores the line suffix Pattern.</summary>
    private static readonly Regex LineSuffixPattern = new("^(?<path>.+):(?<line>\\d+)$", RegexOptions.Compiled);

    /// <summary>Initializes a new instance of the <see cref="SelectableMarkdownViewer"/> class.</summary>
    public SelectableMarkdownViewer()
    {
        IsReadOnly = true;
        IsDocumentEnabled = true;
        BorderThickness = new(0.0);
        Padding = new(0.0);
        Background = Brushes.Transparent;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        MinWidth = 0.0;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        ContextMenu = BuildContextMenu();
    }

    /// <summary>Gets or sets the markdown.</summary>
    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set
        {
            SetValue(MarkdownProperty, value);
        }
    }

    /// <summary>Handles the render Size Changed event.</summary>
    /// <param name="sizeInfo">The size Info.</param>
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateDocumentPageWidth();
    }

    /// <summary>Adds line Break If Needed.</summary>
    /// <param name="paragraph">The paragraph.</param>
    /// <param name="index">The index.</param>
    /// <param name="lineCount">The line Count.</param>
    private static void AddLineBreakIfNeeded(Paragraph paragraph, int index, int lineCount)
    {
        if (index >= lineCount - 1)
        {
            return;
        }

        paragraph.Inlines.Add(new LineBreak());
    }

    /// <summary>Finds next Inline Token.</summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The start.</param>
    /// <returns>The find Next Inline Token result.</returns>
    private static InlineToken FindNextInlineToken(string text, int start)
    {
        InlineToken best = new(-1, InlineKindNone);
        Match link = LinkPattern.Match(text, start);
        if (link.Success)
        {
            best = new(link.Index, InlineKindLink);
        }

        int code = text.IndexOf('`', start);
        if (code >= 0 && text.IndexOf('`', code + 1) > code && (best.Index < 0 || code < best.Index))
        {
            best = new(code, InlineKindCode);
        }

        int bold = text.IndexOf("**", start, StringComparison.Ordinal);
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
        if (!(sender is Hyperlink { Tag: string target }) || string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        try
        {
            string path = StripLineSuffix(target);
            _ = Process.Start(new ProcessStartInfo((File.Exists(path) || Directory.Exists(path)) ? path : target)
            {
                UseShellExecute = true
            });
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
        Match match = LineSuffixPattern.Match(target);
        return !match.Success || !File.Exists(match.Groups["path"].Value) ? target : match.Groups["path"].Value;
    }

    /// <summary>Handles the markdown Changed event.</summary>
    /// <param name="d">The d.</param>
    /// <param name="e">The e.</param>
    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SelectableMarkdownViewer)d).Render(((string)e.NewValue) ?? string.Empty);
    }

    /// <summary>Renders the operation.</summary>
    /// <param name="markdown">The markdown.</param>
    private void Render(string markdown)
    {
        FlowDocument document = new FlowDocument
        {
            PagePadding = new(0.0),
            Background = Brushes.Transparent,
            MinPageWidth = 0.0,
            LineHeight = Numeric18Point0
        };
        BindDocumentToViewer(document);
        Paragraph paragraph = new Paragraph
        {
            Margin = new(0.0)
        };
        document.Blocks.Add(paragraph);
        string[] lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        bool inCodeBlock = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                inCodeBlock = !inCodeBlock;
                AddLineBreakIfNeeded(paragraph, i, lines.Length);
                continue;
            }

            if (inCodeBlock)
            {
                Run codeRun = new(line)
                {
                    FontFamily = new("Consolas")
                };
                codeRun.FontSize = FontSize;
                codeRun.FontSize *= Numeric0Point96;
                paragraph.Inlines.Add(codeRun);
            }
            else if (trimmed.Length > 0)
            {
                RenderMarkdownLine(paragraph, line);
            }

            AddLineBreakIfNeeded(paragraph, i, lines.Length);
        }

        Document = document;
        UpdateDocumentPageWidth();
    }

    /// <summary>Performs the bind Document To Viewer operation.</summary>
    /// <param name="document">The document.</param>
    private void BindDocumentToViewer(FlowDocument document)
    {
        _ = BindingOperations.SetBinding(document, TextElement.ForegroundProperty, CreateViewerBinding(nameof(Foreground)));
        _ = BindingOperations.SetBinding(document, TextElement.FontFamilyProperty, CreateViewerBinding(nameof(FontFamily)));
        _ = BindingOperations.SetBinding(document, TextElement.FontSizeProperty, CreateViewerBinding(nameof(FontSize)));
    }

    /// <summary>Creates viewer Binding.</summary>
    /// <param name="propertyName">The property Name.</param>
    /// <returns>The create Viewer Binding result.</returns>
    private Binding CreateViewerBinding(string propertyName)
    {
        return new(propertyName)
        {
            Mode = BindingMode.OneWay,
            Source = this
        };
    }

    /// <summary>Updates document Page Width.</summary>
    private void UpdateDocumentPageWidth()
    {
        if (Document is null)
        {
            return;
        }

        double availableWidth = ActualWidth - Padding.Left - Padding.Right - BorderThickness.Left - BorderThickness.Right;
        if ((availableWidth <= 0.0) || double.IsNaN(availableWidth) || double.IsInfinity(availableWidth))
        {
            return;
        }

        Document.PageWidth = availableWidth;
    }

    /// <summary>Renders markdown Line.</summary>
    /// <param name="paragraph">The paragraph.</param>
    /// <param name="line">The line.</param>
    private void RenderMarkdownLine(Paragraph paragraph, string line)
    {
        string trimmed = line.TrimStart();
        if (trimmed.StartsWith("# ", StringComparison.Ordinal))
        {
            AddInlineRuns(paragraph, trimmed.Substring(Numeric2), FontWeights.SemiBold, FontStyles.Normal, FontSize + Numeric2);
            return;
        }

        if (trimmed.StartsWith("## ", StringComparison.Ordinal))
        {
            AddInlineRuns(paragraph, trimmed.Substring(Numeric3), FontWeights.SemiBold, FontStyles.Normal, FontSize + 1.0);
            return;
        }

        if (trimmed.StartsWith("**", StringComparison.Ordinal) && trimmed.EndsWith("**", StringComparison.Ordinal) && trimmed.Length > Numeric4)
        {
            AddInlineRuns(paragraph, trimmed.Substring(Numeric2, trimmed.Length - Numeric4), FontWeights.SemiBold, FontStyles.Normal, FontSize);
            return;
        }

        Match bullet = BulletPattern.Match(line);
        if (bullet.Success)
        {
            paragraph.Inlines.Add(new Run("- ")
            {
                FontWeight = FontWeights.SemiBold
            });
            AddInlineRuns(paragraph, line.Substring(bullet.Length), FontWeights.Normal, FontStyles.Normal, FontSize);
            return;
        }

        Match number = NumberedPattern.Match(line);
        if (number.Success)
        {
            paragraph.Inlines.Add(new Run($"{number.Groups[1].Value} ")
            {
                FontWeight = FontWeights.SemiBold
            });
            AddInlineRuns(paragraph, line.Substring(number.Length), FontWeights.Normal, FontStyles.Normal, FontSize);
        }
        else
        {
            AddInlineRuns(paragraph, line.TrimStart(), FontWeights.Normal, FontStyles.Normal, FontSize);
        }
    }

    /// <summary>Builds context Menu.</summary>
    /// <returns>The build Context Menu result.</returns>
    private ContextMenu BuildContextMenu()
    {
        return new ContextMenu
        {
            Items =
            {
                (object)new MenuItem
                {
                    Header = "Copy",
                    Command = ApplicationCommands.Copy,
                    CommandTarget = this
                },
                (object)new MenuItem
                {
                    Header = "Select all",
                    Command = ApplicationCommands.SelectAll,
                    CommandTarget = this
                }
            }
        };
    }

    /// <summary>Adds inline Runs.</summary>
    /// <param name="paragraph">The paragraph.</param>
    /// <param name="text">The text.</param>
    /// <param name="weight">The weight.</param>
    /// <param name="style">The style.</param>
    /// <param name="size">The size.</param>
    private void AddInlineRuns(Paragraph paragraph, string text, FontWeight weight, FontStyle style, double size)
    {
        int index = 0;
        while (index < text.Length)
        {
            InlineToken next = FindNextInlineToken(text, index);
            if (next.Index < 0)
            {
                AddRun(paragraph, text.Remove(0, index), weight, style, size, code: false);
                break;
            }

            if (next.Index > index)
            {
                AddRun(paragraph, text.Substring(index, next.Index - index), weight, style, size, code: false);
            }

            if (next.Kind == InlineKindLink)
            {
                Match match = LinkPattern.Match(text, next.Index);
                AddHyperlink(paragraph, match.Groups[1].Value, match.Groups[Numeric2].Value.Trim());
                index = match.Index + match.Length;
            }
            else if (next.Kind == InlineKindCode)
            {
                int end = text.IndexOf('`', next.Index + 1);
                AddRun(paragraph, text.Substring(next.Index + 1, end - next.Index - 1), FontWeights.Normal, FontStyles.Normal, size, code: true);
                index = end + 1;
            }
            else
            {
                int end2 = text.IndexOf("**", next.Index + Numeric2, StringComparison.Ordinal);
                AddInlineRuns(paragraph, text.Substring(next.Index + Numeric2, end2 - next.Index - Numeric2), FontWeights.SemiBold, style, size);
                index = end2 + Numeric2;
            }
        }
    }

    /// <summary>Adds run.</summary>
    /// <param name="paragraph">The paragraph.</param>
    /// <param name="text">The text.</param>
    /// <param name="weight">The weight.</param>
    /// <param name="style">The style.</param>
    /// <param name="size">The size.</param>
    /// <param name="code">The code.</param>
    private void AddRun(Paragraph paragraph, string text, FontWeight weight, FontStyle style, double size, bool code)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        paragraph.Inlines.Add(new Run(text)
        {
            FontFamily = code ? new FontFamily("Consolas") : FontFamily,
            FontSize = (code ? (size * Numeric0Point96) : size),
            FontStyle = style,
            FontWeight = weight
        });
    }

    /// <summary>Adds hyperlink.</summary>
    /// <param name="paragraph">The paragraph.</param>
    /// <param name="label">The label.</param>
    /// <param name="target">The target.</param>
    private void AddHyperlink(Paragraph paragraph, string label, string target)
    {
        Hyperlink hyperlink = new(new Run(label));
        hyperlink.Cursor = Cursors.Hand;
        hyperlink.Tag = target.Trim('<', '>');
        Hyperlink link = hyperlink;
        link.Click += OnHyperlinkClick;
        paragraph.Inlines.Add(link);
    }

    /// <summary>Provides the inline Token implementation.</summary>
    private readonly struct InlineToken
    {
        /// <summary>Initializes a new instance of the <see cref="InlineToken"/> struct.</summary>
        /// <param name="index">The token start index.</param>
        /// <param name="kind">The token kind.</param>
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
