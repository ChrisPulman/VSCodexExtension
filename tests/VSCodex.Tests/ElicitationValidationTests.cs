// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Newtonsoft.Json.Linq;
using VSCodex.Models;

namespace VSCodex.Tests;

/// <summary>Verifies constraints that would otherwise send invalid MCP form responses.</summary>
public sealed class ElicitationValidationTests
{
    /// <summary>Validates user values against server-supplied primitive constraints.</summary>
    /// <param name="schema">The field schema.</param>
    /// <param name="input">The user value.</param>
    /// <param name="valid">The expected validation result.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    [Arguments("{type:'string',minLength:2}", "a", false)]
    [Arguments("{type:'string',maxLength:1}", "ab", false)]
    [Arguments("{type:'string',maxLength:1}", "😀", true)]
    [Arguments("{type:'string',minLength:2}", "😀", false)]
    [Arguments("{type:'string',pattern:'^[A-Z]+$'}", "ABC", true)]
    [Arguments("{type:'string',pattern:'^[A-Z]+$'}", "abc", false)]
    [Arguments("{type:'string',pattern:'['}", "abc", false)]
    [Arguments("{type:'string',format:'uri'}", "https://example.com/test", true)]
    [Arguments("{type:'string',format:'uri'}", "relative/path", false)]
    [Arguments("{type:'string',format:'email'}", "person@example.com", true)]
    [Arguments("{type:'string',format:'email'}", "person", false)]
    [Arguments("{type:'string',format:'date'}", "2024-02-29", true)]
    [Arguments("{type:'string',format:'date'}", "2023-02-29", false)]
    [Arguments("{type:'string',format:'date-time'}", "2026-10-23T12:34:56Z", true)]
    [Arguments("{type:'string',format:'date-time'}", "2026-10-23", false)]
    [Arguments("{type:'string',format:'date-time'}", "2026-10-23T12:34:56", false)]
    [Arguments("{type:'integer',minimum:2}", "1", false)]
    [Arguments("{type:'integer',maximum:2}", "3", false)]
    [Arguments("{type:'number',exclusiveMinimum:2}", "2", false)]
    [Arguments("{type:'number',exclusiveMaximum:2}", "2", false)]
    [Arguments("{type:'number',multipleOf:0.5}", "1.5", true)]
    [Arguments("{type:'number',multipleOf:0.5}", "1.6", false)]
    [Arguments("{type:'number',multipleOf:0}", "1", false)]
    [Arguments("{type:'boolean'}", "invalid", false)]
    [Arguments("{type:'integer'}", "1.5", false)]
    [Arguments("{type:'number'}", "NaN", false)]
    public async Task Primitive_constraints_validate_before_submission(string schema, string input, bool valid)
    {
        var field = new ElicitationField("value", JObject.Parse(schema), true) { Value = input };
        bool actual = field.TryRead(out _, out string error);
        await Assert.That(actual).IsEqualTo(valid);
        await Assert.That(string.IsNullOrEmpty(error)).IsEqualTo(valid);
    }
}
