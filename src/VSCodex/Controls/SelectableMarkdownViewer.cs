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

public sealed class SelectableMarkdownViewer : RichTextBox
{
    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.Register(nameof(Markdown), typeof(string), typeof(SelectableMarkdownViewer), new PropertyMetadata(string.Empty, OnMarkdownChanged));

    private static readonly Regex LinkPattern = new Regex(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex BulletPattern = new Regex(@"^\s*[-*]\s+", RegexOptions.Compiled);
    private static readonly Regex NumberedPattern = new Regex(@"^\s*(\d+\.)\s+", RegexOptions.Compiled);

    public SelectableMarkdownViewer()
    {
        IsReadOnly = true;
        IsDocumentEnabled = true;
        BorderThickness = new Thickness(0);
        Padding = new Thickness(0);
        Background = Brushes.Transparent;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        ContextMenu = BuildContextMenu();
    }

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SelectableMarkdownViewer)d).Render((string?)e.NewValue ?? string.Empty);
    }

    private void Render(string markdown)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            Background = Brushes.Transparent,
            FontFamily = FontFamily,
            FontSize = FontSize,
            Foreground = Foreground,
            LineHeight = 18
        };

        var paragraph = new Paragraph { Margin = new Thickness(0) };
        document.Blocks.Add(paragraph);

        var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var inCodeBlock = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                inCodeBlock = !inCodeBlock;
                AddLineBreakIfNeeded(paragraph, i, lines.Length);
                continue;
            }

            if (inCodeBlock)
            {
                paragraph.Inlines.Add(new Run(line)
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = FontSize * 0.96
                });
            }
            else if (trimmed.Length > 0)
            {
                RenderMarkdownLine(paragraph, line);
            }

            AddLineBreakIfNeeded(paragraph, i, lines.Length);
        }

        Document = document;
    }

    private static void AddLineBreakIfNeeded(Paragraph paragraph, int index, int lineCount)
    {
        if (index < lineCount - 1)
        {
            paragraph.Inlines.Add(new LineBreak());
        }
    }

    private void RenderMarkdownLine(Paragraph paragraph, string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("# ", StringComparison.Ordinal))
        {
            AddInlineRuns(paragraph, trimmed.Substring(2), FontWeights.SemiBold, FontStyles.Normal, FontSize + 2);
            return;
        }

        if (trimmed.StartsWith("## ", StringComparison.Ordinal))
        {
            AddInlineRuns(paragraph, trimmed.Substring(3), FontWeights.SemiBold, FontStyles.Normal, FontSize + 1);
            return;
        }

        if (trimmed.StartsWith("**", StringComparison.Ordinal) && trimmed.EndsWith("**", StringComparison.Ordinal) && trimmed.Length > 4)
        {
            AddInlineRuns(paragraph, trimmed.Substring(2, trimmed.Length - 4), FontWeights.SemiBold, FontStyles.Normal, FontSize);
            return;
        }

        var bullet = BulletPattern.Match(line);
        if (bullet.Success)
        {
            paragraph.Inlines.Add(new Run("- ") { FontWeight = FontWeights.SemiBold });
            AddInlineRuns(paragraph, line.Substring(bullet.Length), FontWeights.Normal, FontStyles.Normal, FontSize);
            return;
        }

        var number = NumberedPattern.Match(line);
        if (number.Success)
        {
            paragraph.Inlines.Add(new Run(number.Groups[1].Value + " ") { FontWeight = FontWeights.SemiBold });
            AddInlineRuns(paragraph, line.Substring(number.Length), FontWeights.Normal, FontStyles.Normal, FontSize);
            return;
        }

        AddInlineRuns(paragraph, line.TrimStart(), FontWeights.Normal, FontStyles.Normal, FontSize);
    }

    private ContextMenu BuildContextMenu()
    {
        return new ContextMenu
        {
            Items =
            {
                new MenuItem { Header = "Copy", Command = ApplicationCommands.Copy, CommandTarget = this },
                new MenuItem { Header = "Select all", Command = ApplicationCommands.SelectAll, CommandTarget = this }
            }
        };
    }

    private void AddInlineRuns(Paragraph paragraph, string text, FontWeight weight, FontStyle style, double size)
    {
        var index = 0;
        while (index < text.Length)
        {
            var next = FindNextInlineToken(text, index);
            if (next.Index < 0)
            {
                AddRun(paragraph, text.Substring(index), weight, style, size, false);
                break;
            }

            if (next.Index > index)
            {
                AddRun(paragraph, text.Substring(index, next.Index - index), weight, style, size, false);
            }

            if (next.Kind == InlineKind.Link)
            {
                var match = LinkPattern.Match(text, next.Index);
                AddHyperlink(paragraph, match.Groups[1].Value, match.Groups[2].Value.Trim());
                index = match.Index + match.Length;
            }
            else if (next.Kind == InlineKind.Code)
            {
                var end = text.IndexOf('`', next.Index + 1);
                AddRun(paragraph, text.Substring(next.Index + 1, end - next.Index - 1), FontWeights.Normal, FontStyles.Normal, size, true);
                index = end + 1;
            }
            else
            {
                var end = text.IndexOf("**", next.Index + 2, StringComparison.Ordinal);
                AddInlineRuns(paragraph, text.Substring(next.Index + 2, end - next.Index - 2), FontWeights.SemiBold, style, size);
                index = end + 2;
            }
        }
    }

    private void AddRun(Paragraph paragraph, string text, FontWeight weight, FontStyle style, double size, bool code)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        paragraph.Inlines.Add(new Run(text)
        {
            FontFamily = code ? new FontFamily("Consolas") : FontFamily,
            FontSize = code ? size * 0.96 : size,
            FontStyle = style,
            FontWeight = weight
        });
    }

    private void AddHyperlink(Paragraph paragraph, string label, string target)
    {
        var link = new Hyperlink(new Run(label))
        {
            Cursor = Cursors.Hand,
            Tag = target.Trim('<', '>')
        };
        link.Click += OnHyperlinkClick;
        paragraph.Inlines.Add(link);
    }

    private static InlineToken FindNextInlineToken(string text, int start)
    {
        var best = new InlineToken(-1, InlineKind.None);
        var link = LinkPattern.Match(text, start);
        if (link.Success)
        {
            best = new InlineToken(link.Index, InlineKind.Link);
        }

        var code = text.IndexOf('`', start);
        if (code >= 0 && text.IndexOf('`', code + 1) > code && (best.Index < 0 || code < best.Index))
        {
            best = new InlineToken(code, InlineKind.Code);
        }

        var bold = text.IndexOf("**", start, StringComparison.Ordinal);
        if (bold >= 0 && text.IndexOf("**", bold + 2, StringComparison.Ordinal) > bold && (best.Index < 0 || bold < best.Index))
        {
            best = new InlineToken(bold, InlineKind.Bold);
        }

        return best;
    }

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
            Process.Start(new ProcessStartInfo(targetToOpen) { UseShellExecute = true });
        }
        catch
        {
            // Link activation is best-effort; message rendering must remain stable.
        }
    }

    private static string StripLineSuffix(string target)
    {
        var match = Regex.Match(target, @"^(?<path>.+):(?<line>\d+)$");
        return match.Success && File.Exists(match.Groups["path"].Value) ? match.Groups["path"].Value : target;
    }

    private readonly struct InlineToken
    {
        public InlineToken(int index, InlineKind kind)
        {
            Index = index;
            Kind = kind;
        }

        public int Index { get; }
        public InlineKind Kind { get; }
    }

    private enum InlineKind { None, Link, Code, Bold }
}
