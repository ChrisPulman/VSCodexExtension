// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using VSCodex.Core.Presentation;

namespace VSCodex.Tests;

/// <summary>Exercises completion visibility independently of WPF rendering.</summary>
public sealed class ResponsePresentationTests
{
    /// <summary>Counts retained commentary and its final summary.</summary>
    private const int CommentaryAndSummaryCount = 2;

    /// <summary>Preserves commentary while displaying a separate final summary exactly once.</summary>
    /// <param name="streamed">The accumulated transport deltas.</param>
    /// <param name="final">The completed transport response.</param>
    /// <param name="earlier">The expected retained commentary.</param>
    /// <param name="summary">The expected expanded summary.</param>
    /// <returns>A task that completes after response assertions.</returns>
    [Test]
    [Arguments("Investigating.\nDone.", "Done.", "Investigating.\n", "Done.")]
    [Arguments("Investigating.", "Done.", "Investigating.", "Done.")]
    [Arguments("Done.", "Done.", null, "Done.")]
    [Arguments("  \nDone.", "Done.", null, "Done.")]
    [Arguments("", "Done.", null, "Done.")]
    [Arguments("Available streamed answer", "", null, "Available streamed answer")]
    [Arguments("Available streamed answer", null, null, "Available streamed answer")]
    [Arguments(null, "Done.", null, "Done.")]
    [Arguments("Done. was an earlier observation.\nDone.", "Done.", "Done. was an earlier observation.\n", "Done.")]
    public async Task StreamCompletionPreservesEarlierTextAndRevealsSummary(
        string? streamed,
        string? final,
        string? earlier,
        string summary)
    {
        IReadOnlyList<string> completed = ResponsePresentation.GetCompletedResponses(streamed, final);
        Node[] nodes = completed.Select(text => new Node { Text = text, Expanded = true }).ToArray();
        bool revealed = false;
        ResponsePresentation.Complete(nodes, node => node.Text, (node, expanded) => node.Expanded = expanded, () => revealed = true);
        await Assert.That(nodes.Length).IsEqualTo(earlier is null ? 1 : CommentaryAndSummaryCount);
        await Assert.That(nodes[^1].Text).IsEqualTo(summary);
        await Assert.That(nodes[^1].Expanded).IsTrue();
        await Assert.That(revealed).IsTrue();
        if (earlier is null)
        {
            return;
        }

        await Assert.That(nodes[0].Text).IsEqualTo(earlier);
        await Assert.That(nodes[0].Expanded).IsFalse();
    }

    /// <summary>Reveals a final summary through collapsed ancestors without closing inspected tools.</summary>
    /// <returns>A task that completes after presentation assertions.</returns>
    [Test]
    public async Task CompletionRevealsFinalSummaryAndPreservesToolExpansion()
    {
        Node commentary = new() { Text = "Investigating", Expanded = true };
        Node summary = new() { Text = "Implemented and verified", Expanded = false };
        Node emptyStream = new() { Text = "  ", Expanded = true };
        Node tool = new() { Text = "User opened this tool", Expanded = true };
        Node mcp = new() { Text = "MCP output", Expanded = false };
        bool rootExpanded = false;
        bool responseSectionExpanded = false;
        ResponsePresentation.Complete(
            new[] { commentary, summary, emptyStream },
            node => node.Text,
            (node, expanded) => node.Expanded = expanded,
            () =>
            {
                rootExpanded = true;
                responseSectionExpanded = true;
            });
        await Assert.That(summary.Expanded).IsTrue();
        await Assert.That(commentary.Expanded).IsFalse();
        await Assert.That(emptyStream.Expanded).IsFalse();
        await Assert.That(rootExpanded).IsTrue();
        await Assert.That(responseSectionExpanded).IsTrue();
        await Assert.That(tool.Expanded).IsTrue();
        await Assert.That(mcp.Expanded).IsFalse();
    }

    /// <summary>An empty or failed response cannot reveal a fabricated summary.</summary>
    /// <returns>A task that completes after presentation assertions.</returns>
    [Test]
    public async Task CompletionWithoutTextLeavesAncestorsAlone()
    {
        Node empty = new() { Expanded = true };
        bool revealed = false;
        ResponsePresentation.Complete(new[] { empty }, node => node.Text, (node, expanded) => node.Expanded = expanded, () => revealed = true);
        await Assert.That(empty.Expanded).IsFalse();
        await Assert.That(revealed).IsFalse();
    }

    /// <summary>Provides a framework-independent host node.</summary>
    private sealed class Node
    {
        /// <summary>Gets or sets displayed text.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets expansion state.</summary>
        public bool Expanded { get; set; }
    }
}
