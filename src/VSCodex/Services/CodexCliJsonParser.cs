// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Accumulates the public Codex exec JSONL protocol independently of process I/O.</summary>
/// <param name="threadId">The resumed thread identifier.</param>
internal sealed class CodexCliJsonParser(string? threadId = null)
{
    /// <summary>Defines a progress event.</summary>
    private const string ProgressType = "progress";

    /// <summary>Defines an item completion event.</summary>
    private const string ItemCompleted = "item.completed";

    /// <summary>Defines the item event prefix length.</summary>
    private const int ItemPrefixLength = 5;

    /// <summary>Stores raw protocol output.</summary>
    private readonly StringBuilder _raw = new();

    /// <summary>Stores the last text for each assistant item.</summary>
    private readonly Dictionary<string, string> _messages = new();

    /// <summary>Gets the persisted CLI thread identifier.</summary>
    internal string? ThreadId { get; private set; } = threadId;

    /// <summary>Gets the last completed assistant message.</summary>
    internal string FinalResponse { get; private set; } = string.Empty;

    /// <summary>Gets the complete protocol transcript.</summary>
    internal string RawJson => _raw.ToString();

    /// <summary>Gets a terminal failure, if reported.</summary>
    internal string? Failure { get; private set; }

    /// <summary>Gets whether the turn reached successful completion.</summary>
    internal bool Completed { get; private set; }

    /// <summary>Parses one protocol line and maps events onto the existing UI contract.</summary>
    /// <param name="line">The JSONL line.</param>
    /// <returns>UI events for the line.</returns>
    internal IReadOnlyList<CodexEvent> ParseLine(string line)
    {
        var events = new List<CodexEvent>();
        if (string.IsNullOrWhiteSpace(line))
        {
            return events;
        }

        _ = _raw.AppendLine(line);
        try
        {
            ParseEvent(JObject.Parse(line), line, events);
        }
        catch (JsonException)
        {
            Failure = "Codex CLI emitted invalid JSON output.";
            events.Add(CreateEvent(ProgressType, Failure, line));
        }

        return events;
    }

    /// <summary>Maps one structured event.</summary>
    /// <param name="json">The event payload.</param>
    /// <param name="line">The raw event.</param>
    /// <param name="events">The resulting UI events.</param>
    private void ParseEvent(JObject json, string line, List<CodexEvent> events)
    {
        string? type = json.Value<string>("type");
        if (type == "thread.started")
        {
            ThreadId = json.Value<string>("thread_id") ?? ThreadId;
            events.Add(CreateEvent(ProgressType, "Started Codex thread", line));
            return;
        }

        if (type == "turn.started")
        {
            Completed = false;
            events.Add(CreateEvent("turn-started", "Codex turn started", line));
            return;
        }

        if (type == "turn.completed")
        {
            Completed = true;
            events.Add(CreateEvent(ProgressType, "Codex turn completed", line));
            return;
        }

        ParseFailureOrItem(json, type, line, events);
    }

    /// <summary>Maps failure and item messages.</summary>
    /// <param name="json">The event payload.</param>
    /// <param name="type">The protocol event type.</param>
    /// <param name="line">The raw event.</param>
    /// <param name="events">The resulting UI events.</param>
    private void ParseFailureOrItem(JObject json, string? type, string line, List<CodexEvent> events)
    {
        if (type is "turn.failed" or "error")
        {
            Failure = json["error"]?.Value<string>("message") ?? json.Value<string>("message") ?? "Codex CLI turn failed.";
            events.Add(CreateEvent(ProgressType, Failure, line));
            return;
        }

        if (type is not ("item.started" or "item.updated" or ItemCompleted))
        {
            return;
        }

        ParseItem(json["item"] as JObject, type, line, events);
    }

    /// <summary>Maps a CLI item event.</summary>
    /// <param name="item">The item payload.</param>
    /// <param name="type">The protocol event type.</param>
    /// <param name="line">The raw event.</param>
    /// <param name="events">The resulting UI events.</param>
    private void ParseItem(JObject? item, string type, string line, List<CodexEvent> events)
    {
        string itemType = item?.Value<string>("type") ?? "item";
        if (itemType != "agent_message" || item is null)
        {
            events.Add(CreateEvent(ProgressType, $"Codex {type.Remove(0, ItemPrefixLength)} {itemType}", line));
            return;
        }

        ParseAssistantItem(item, type, line, events);
    }

    /// <summary>Accumulates an assistant message.</summary>
    /// <param name="item">The item.</param>
    /// <param name="type">The protocol event type.</param>
    /// <param name="line">The raw event.</param>
    /// <param name="events">The resulting UI events.</param>
    private void ParseAssistantItem(JObject item, string type, string line, List<CodexEvent> events)
    {
        string id = item.Value<string>("id") ?? string.Empty;
        string text = item.Value<string>("text") ?? string.Empty;
        _ = _messages.TryGetValue(id, out string? previous);
        _messages[id] = text;
        if (type == ItemCompleted)
        {
            FinalResponse = text;
        }

        if (!text.StartsWith(previous ?? string.Empty, StringComparison.Ordinal))
        {
            return;
        }

        string delta = text.Remove(0, previous?.Length ?? 0);
        if (delta.Length == 0)
        {
            return;
        }

        events.Add(CreateEvent("assistant-delta", delta, line));
    }

    /// <summary>Creates a UI protocol event.</summary>
    /// <param name="type">The event type.</param>
    /// <param name="message">The displayed content.</param>
    /// <param name="rawJson">The raw protocol line.</param>
    /// <returns>The event.</returns>
    private CodexEvent CreateEvent(string type, string message, string rawJson) => new()
    {
        Type = type,
        Message = message,
        ThreadId = ThreadId,
        RawJson = rawJson
    };
}
