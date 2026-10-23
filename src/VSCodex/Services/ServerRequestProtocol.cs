// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using Newtonsoft.Json.Linq;

namespace VSCodex.Services;

/// <summary>Builds typed JSON-RPC server responses without stringifying identifiers.</summary>
public static class ServerRequestProtocol
{
    /// <summary>Builds the bridge command retaining the exact JSON ID type.</summary>
    /// <param name="requestId">The requestId value.</param>
    /// <param name="result">The result value.</param>
    /// <returns>The operation result.</returns>
    public static JObject CreateBridgeCommand(JToken requestId, JObject result)
    {
        if (requestId is null || requestId.Type == JTokenType.Null || (requestId.Type != JTokenType.String && requestId.Type != JTokenType.Integer && requestId.Type != JTokenType.Float))
        {
            throw new ArgumentException("A string or numeric server request identifier is required.", nameof(requestId));
        }

        return new JObject { ["command"] = "respondServerRequest", [nameof(requestId)] = requestId.DeepClone(), [nameof(result)] = result.DeepClone() };
    }

    /// <summary>Builds the response shape required by approval or MCP elicitation.</summary>
    /// <param name="method">The method value.</param>
    /// <param name="approve">The approve value.</param>
    /// <param name="content">The content value.</param>
    /// <param name="cancel">The cancel value.</param>
    /// <returns>The operation result.</returns>
    public static JObject CreateResult(string method, bool approve, JObject? content, bool cancel)
    {
        string decision = approve ? "accept" : "decline";
        if (!method.Equals("mcpServer/elicitation/request", StringComparison.Ordinal))
        {
            return new JObject { [nameof(decision)] = decision };
        }

        JToken responseContent = JValue.CreateNull();
        if (approve && !cancel)
        {
            responseContent = content?.DeepClone() ?? new JObject();
        }

        return new JObject { ["action"] = cancel ? "cancel" : decision, [nameof(content)] = responseContent };
    }
}
