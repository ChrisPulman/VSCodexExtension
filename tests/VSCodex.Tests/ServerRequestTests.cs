// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Newtonsoft.Json.Linq;
using VSCodex.Models;
using VSCodex.Services;

namespace VSCodex.Tests;

/// <summary>Exercises the actual protocol and form code used by the extension.</summary>
public sealed class ServerRequestTests
{
    /// <summary>Protocol or schema value answer.</summary>
    private const string AnswerValue = "answer";

    /// <summary>Protocol or schema value mcpServer/elicitation/request.</summary>
    private const string McpServerelicitationrequestValue = "mcpServer/elicitation/request";

    /// <summary>Protocol or schema value requestId.</summary>
    private const string RequestIdValue = "requestId";

    /// <summary>Protocol or schema value content.</summary>
    private const string ContentValue = "content";

    /// <summary>Protocol or schema value action.</summary>
    private const string ActionValue = "action";

    /// <summary>Test answer value.</summary>
    private const int OriginalAnswer = 42;

    /// <summary>Test answer value.</summary>
    private const int ChangedAnswer = 99;

    /// <summary>Numeric zero remains numeric through a serialized bridge response.</summary>
    /// <returns>A task that completes after assertions.</returns>
    [Test]
    public async Task BridgePreservesNumericZeroAndClonesPayload()
    {
        var content = new JObject { [AnswerValue] = OriginalAnswer };
        JObject result = ServerRequestProtocol.CreateResult(McpServerelicitationrequestValue, true, content, false);
        JObject command = ServerRequestProtocol.CreateBridgeCommand(new JValue(0), result);
        content[AnswerValue] = ChangedAnswer;
        JObject roundTrip = JObject.Parse(command.ToString());
        await Assert.That(roundTrip[RequestIdValue]!.Type).IsEqualTo(JTokenType.Integer);
        await Assert.That(roundTrip.Value<int>(RequestIdValue)).IsEqualTo(0);
        await Assert.That(roundTrip[nameof(result)]![ContentValue]!.Value<int>(AnswerValue)).IsEqualTo(OriginalAnswer);
        await Assert.That(roundTrip.Value<string>("command")).IsEqualTo("respondServerRequest");
    }

    /// <summary>String identifiers must remain distinct from numeric identifiers.</summary>
    /// <returns>A task that completes after assertions.</returns>
    [Test]
    public async Task BridgePreservesStringIdentifier()
    {
        JObject command = ServerRequestProtocol.CreateBridgeCommand(new JValue("0"), new());
        await Assert.That(command[RequestIdValue]!.Type).IsEqualTo(JTokenType.String);
        await Assert.That(command.Value<string>(RequestIdValue)).IsEqualTo("0");
    }

    /// <summary>Numeric identifiers retain their JSON number type even when fractional.</summary>
    /// <returns>A task that completes after assertions.</returns>
    [Test]
    public async Task BridgePreservesFractionalNumericIdentifier()
    {
        JObject command = ServerRequestProtocol.CreateBridgeCommand(JToken.Parse("1.5"), new());
        await Assert.That(command[RequestIdValue]!.Type).IsEqualTo(JTokenType.Float);
        await Assert.That(command[RequestIdValue]!.ToString()).IsEqualTo("1.5");
    }

    /// <summary>Accept supplies an empty object while decline and cancel supply null.</summary>
    /// <returns>A task that completes after assertions.</returns>
    [Test]
    public async Task ElicitationActionsHaveProtocolResponseShapes()
    {
        const string method = McpServerelicitationrequestValue;
        JObject accept = ServerRequestProtocol.CreateResult(method, true, null, false);
        JObject decline = ServerRequestProtocol.CreateResult(method, false, null, false);
        JObject cancel = ServerRequestProtocol.CreateResult(method, false, null, true);
        await Assert.That(accept.Value<string>(ActionValue)).IsEqualTo("accept");
        await Assert.That(accept[ContentValue]!.Type).IsEqualTo(JTokenType.Object);
        await Assert.That(decline.Value<string>(ActionValue)).IsEqualTo("decline");
        await Assert.That(decline[ContentValue]!.Type).IsEqualTo(JTokenType.Null);
        await Assert.That(cancel.Value<string>(ActionValue)).IsEqualTo("cancel");
        await Assert.That(cancel[ContentValue]!.Type).IsEqualTo(JTokenType.Null);
    }

    /// <summary>Form editors produce typed values, defaults, and omit absent optional fields.</summary>
    /// <returns>A task that completes after assertions.</returns>
    [Test]
    public async Task FormBuildsTypedRequiredOptionalAndDefaultValues()
    {
        ElicitationForm form = ParseForm("""
            {"type":"object","properties":{
              "name":{"type":"string","minLength":2},
              "count":{"type":"integer","minimum":1},
              "enabled":{"type":"boolean","default":true},
              "choice":{"type":"string","enum":["one","two"]},
              "optional":{"type":"string"}},"required":["name","count","choice"]}
            """);
        form.Fields[0].Value = "Chris";
        form.Fields[1].Value = "2";
        form.Fields[3].Value = "two";
        bool valid = form.TryBuildContent(out JObject? content, out string error);
        await Assert.That(valid).IsTrue();
        await Assert.That(error).IsEqualTo(string.Empty);
        await Assert.That(content!.Value<string>("name")).IsEqualTo("Chris");
        await Assert.That(content["count"]!.Type).IsEqualTo(JTokenType.Integer);
        await Assert.That(content.Value<bool>("enabled")).IsTrue();
        await Assert.That(content.ContainsKey("optional")).IsFalse();
    }

    /// <summary>Required values and schema constraints block only acceptance with actionable feedback.</summary>
    /// <returns>A task that completes after assertions.</returns>
    [Test]
    public async Task FormValidatesRequiredNumericAndEnumConstraints()
    {
        ElicitationForm form = ParseForm("""{"type":"object","properties":{"count":{"type":"integer","minimum":1,"maximum":5}},"required":["count"]}""");
        await Assert.That(form.TryBuildContent(out _, out string requiredError)).IsFalse();
        await Assert.That(requiredError.Contains("count", StringComparison.Ordinal)).IsTrue();
        form.Fields[0].Value = "6";
        await Assert.That(form.TryBuildContent(out _, out _)).IsFalse();
        form.Fields[0].Value = "2.5";
        await Assert.That(form.TryBuildContent(out _, out _)).IsFalse();
        form.Fields[0].Value = "3";
        await Assert.That(form.TryBuildContent(out _, out _)).IsTrue();
        ElicitationForm choice = ParseForm("""{"type":"object","properties":{"value":{"type":"string","enum":["a","b"]}},"required":["value"]}""");
        choice.Fields[0].Value = "invalid";
        await Assert.That(choice.TryBuildContent(out _, out _)).IsFalse();
    }

    /// <summary>Empty forms can be accepted, unsupported schemas can still be declined or cancelled.</summary>
    /// <returns>A task that completes after assertions.</returns>
    [Test]
    public async Task EmptyAndUnsupportedFormsRemainActionable()
    {
        ElicitationForm empty = ParseForm("""{"type":"object","properties":{}}""");
        await Assert.That(empty.TryBuildContent(out JObject? content, out _)).IsTrue();
        await Assert.That(content!.Count).IsEqualTo(0);
        ElicitationForm nested = ParseForm("""{"type":"object","properties":{"nested":{"type":"object","properties":{}}}}""");
        await Assert.That(nested.TryBuildContent(out _, out string error)).IsFalse();
        await Assert.That(error.Contains("Decline or Cancel", StringComparison.Ordinal)).IsTrue();
        JObject decline = ServerRequestProtocol.CreateResult(McpServerelicitationrequestValue, false, null, false);
        await Assert.That(decline.Value<string>(ActionValue)).IsEqualTo("decline");
        ElicitationForm url = ElicitationForm.Create(new JObject { ["mode"] = "url" });
        await Assert.That(url.TryBuildContent(out _, out _)).IsFalse();
    }

    /// <summary>Malformed constraints remain actionable and required strings may legally be empty.</summary>
    /// <returns>A task that completes after assertions.</returns>
    [Test]
    public async Task MalformedConstraintsAndRequiredEmptyStringsFollowSchemaSemantics()
    {
        ElicitationForm malformed = ParseForm("""{"type":"object","properties":{"value":{"type":"number","minimum":"invalid"}},"required":["value"]}""");
        malformed.Fields[0].Value = "1";
        await Assert.That(malformed.TryBuildContent(out _, out string error)).IsFalse();
        await Assert.That(error.Contains("Decline or Cancel", StringComparison.Ordinal)).IsTrue();
        ElicitationForm emptyString = ParseForm("""{"type":"object","properties":{"value":{"type":"string"}},"required":["value"]}""");
        await Assert.That(emptyString.TryBuildContent(out JObject? content, out _)).IsTrue();
        await Assert.That(content!.Value<string>("value")).IsEqualTo(string.Empty);
    }

    /// <summary>Allows optional empty strings to be explicitly included or omitted.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task OptionalEmptyStringCanBeIncludedExplicitly()
    {
        var field = new ElicitationField("value", JObject.Parse("{type:'string',enum:['','text']}"), false);
        await Assert.That(field.TryRead(out JToken? omitted, out _)).IsTrue();
        await Assert.That(omitted).IsNull();
        field.IncludeEmptyValue = true;
        await Assert.That(field.TryRead(out JToken? included, out _)).IsTrue();
        await Assert.That(included!.Type).IsEqualTo(JTokenType.String);
        await Assert.That(included.Value<string>()).IsEqualTo(string.Empty);
    }

    /// <summary>Performs ParseForm.</summary>
    /// <param name="schema">The schema value.</param>
    /// <returns>The operation result.</returns>
    private static ElicitationForm ParseForm(string schema) => ElicitationForm.Create(new JObject { ["mode"] = "form", ["requestedSchema"] = JObject.Parse(schema) });
}
