// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VSCodex.Services;

/// <summary>Separates a service failure explanation from transport diagnostics.</summary>
internal static class RunFailureMessageFormatter
{
    /// <summary>Gets a readable explanation while retaining full exceptions in the activity log.</summary>
    /// <param name="exception">The execution failure.</param>
    /// <returns>The service explanation without transport stack frames.</returns>
    internal static string Format(Exception exception)
    {
        var message = exception.GetBaseException().Message.Trim();
        var jsonStart = message.IndexOf('{');
        var jsonEnd = message.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            try
            {
                var payload = JObject.Parse(message.Substring(jsonStart, jsonEnd - jsonStart + 1));
                var explanation = (payload["error"] as JObject)?[nameof(message)] ?? payload[nameof(message)];
                if (explanation?.Type == JTokenType.String && !string.IsNullOrWhiteSpace(explanation.Value<string>()))
                {
                    message = explanation.Value<string>()!;
                }
            }
            catch (JsonException)
            {
                // Non-JSON service messages remain readable plain text.
            }
        }

        var newline = message.IndexOfAny(['\r', '\n']);
        if (newline >= 0)
        {
            message = message.Remove(newline);
        }

        return string.IsNullOrWhiteSpace(message) ? "Codex could not finish this request." : message;
    }
}
