// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;

namespace VSCodex.Core.Presentation;

/// <summary>Applies completion defaults only to assistant response content.</summary>
public static class ResponsePresentation
{
    /// <summary>Separates earlier streamed commentary from the completed summary without duplicating its suffix.</summary>
    /// <param name="streamedText">The accumulated assistant stream.</param>
    /// <param name="finalResponse">The completed summary, when supplied by the transport.</param>
    /// <returns>Earlier commentary followed by the summary, or the available response alone.</returns>
    public static IReadOnlyList<string> GetCompletedResponses(string? streamedText, string? finalResponse)
    {
        string streamed = streamedText ?? string.Empty;
        string summary = finalResponse ?? string.Empty;
        if (string.IsNullOrWhiteSpace(summary))
        {
            return [streamed];
        }

        string earlier = streamed.EndsWith(summary, StringComparison.Ordinal)
            ? streamed.Substring(0, streamed.Length - summary.Length)
            : streamed;
        return string.IsNullOrWhiteSpace(earlier)
            ? [summary]
            : [earlier, summary];
    }

    /// <summary>Shows the final nonempty response and retains earlier text in collapsed nodes.</summary>
    /// <typeparam name="T">The host's response node type.</typeparam>
    /// <param name="responses">Only the assistant response children, excluding tool sections.</param>
    /// <param name="getText">Reads response text.</param>
    /// <param name="setExpanded">Updates a response node's expansion state.</param>
    /// <param name="showAncestors">Reveals ancestors when a final response exists.</param>
    public static void Complete<T>(IEnumerable<T> responses, Func<T, string?> getText, Action<T, bool> setExpanded, Action showAncestors)
        where T : class
    {
        T[] nodes = responses.ToArray();
        T? final = nodes.LastOrDefault(node => !string.IsNullOrWhiteSpace(getText(node)));
        foreach (T node in nodes)
        {
            setExpanded(node, ReferenceEquals(node, final));
        }

        if (final is null)
        {
            return;
        }

        showAncestors();
    }
}
