// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using VSCodex.Services;

namespace VSCodex.Tests;

/// <summary>Exercises persisted sessions and terminal outcomes in the public exec protocol.</summary>
public sealed class CodexCliJsonParserTests
{
    /// <summary>Preserves the thread and final response without exposing protocol JSON as text.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task Completed_turn_preserves_session_and_assistant_text()
    {
        var parser = new CodexCliJsonParser();
        var started = parser.ParseLine("{\"type\":\"thread.started\",\"thread_id\":\"session-123\"}");
        _ = parser.ParseLine("{\"type\":\"item.completed\",\"item\":{\"id\":\"message-1\",\"type\":\"agent_message\",\"text\":\"Gr\u00fc\u00dfe \u65e5\u672c\u8a9e \ud83d\ude00\"}}");
        _ = parser.ParseLine("{\"type\":\"turn.completed\",\"usage\":{}}");
        await Assert.That(parser.ThreadId).IsEqualTo("session-123");
        await Assert.That(started[0].ThreadId).IsEqualTo("session-123");
        await Assert.That(parser.FinalResponse).IsEqualTo("Gr\u00fc\u00dfe \u65e5\u672c\u8a9e \ud83d\ude00");
        await Assert.That(parser.Completed).IsTrue();
        await Assert.That(parser.Failure).IsNull();
    }

    /// <summary>A completed item can follow updates without duplicating streamed text.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task Message_updates_emit_only_new_text_and_keep_last_completed_response()
    {
        var parser = new CodexCliJsonParser("resumed-session");
        var updated = parser.ParseLine("{\"type\":\"item.updated\",\"item\":{\"id\":\"i1\",\"type\":\"agent_message\",\"text\":\"Hello\"}}");
        var completed = parser.ParseLine("{\"type\":\"item.completed\",\"item\":{\"id\":\"i1\",\"type\":\"agent_message\",\"text\":\"Hello world\"}}");
        await Assert.That(updated[0].Type).IsEqualTo("assistant-delta");
        await Assert.That(completed[0].Message).IsEqualTo(" world");
        await Assert.That(completed[0].ThreadId).IsEqualTo("resumed-session");
        await Assert.That(parser.FinalResponse).IsEqualTo("Hello world");
    }

    /// <summary>Protocol failure remains a failure even when a process subsequently exits normally.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task Failure_and_invalid_output_are_not_successful_turns()
    {
        var parser = new CodexCliJsonParser();
        _ = parser.ParseLine("{\"type\":\"turn.failed\",\"error\":{\"message\":\"quota exceeded\"}}");
        await Assert.That(parser.Failure).IsEqualTo("quota exceeded");
        await Assert.That(parser.Completed).IsFalse();
        var invalid = new CodexCliJsonParser();
        _ = invalid.ParseLine("not json");
        await Assert.That(invalid.Failure).IsEqualTo("Codex CLI emitted invalid JSON output.");
        await Assert.That(invalid.FinalResponse).IsEqualTo(string.Empty);
    }
}
