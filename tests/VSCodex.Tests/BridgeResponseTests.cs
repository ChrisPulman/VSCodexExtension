// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace VSCodex.Tests;

/// <summary>Executes the packaged JavaScript response path with a fake RPC responder.</summary>
public sealed class BridgeResponseTests
{
    /// <summary>Bounds the Node response probe.</summary>
    private const int ProbeTimeoutSeconds = 10;

    /// <summary>Undefined and null are the two missing identifier cases.</summary>
    private const int MissingIdentifierCases = 2;

    /// <summary>Zero is sent numerically, string zero stays a string, and missing IDs fail explicitly.</summary>
    /// <returns>A task that completes after the bridge probe.</returns>
    [Test]
    public async Task BridgeRespondsWithTypedZeroAndRejectsMissingIdentifiers()
    {
        string root = FindRepositoryRoot();
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(root, "tests", "bridge-response-probe.mjs"));
        start.ArgumentList.Add(Path.Combine(root, "src", "VSCodex", "Resources", "codex-bridge.mjs"));
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(ProbeTimeoutSeconds));
        await process.WaitForExitAsync(timeout.Token);
        await Assert.That(process.ExitCode).IsEqualTo(0).Because(await errors);
        JObject result = JObject.Parse(await output);
        JArray responses = (JArray)result["responses"]!;
        await Assert.That(responses[0]!["id"]!.Type).IsEqualTo(JTokenType.Integer);
        await Assert.That(responses[0]!.Value<int>("id")).IsEqualTo(0);
        await Assert.That(responses[1]!["id"]!.Type).IsEqualTo(JTokenType.String);
        await Assert.That(result.Value<int>("rejectedMissing")).IsEqualTo(MissingIdentifierCases);
        await Assert.That(result.Value<string>("activeConflict")!).Contains("already has an active Codex turn");
        await Assert.That(result.Value<int>("activeConflictRpcCalls")).IsEqualTo(0);
        await Assert.That(result.Value<string>("resumeConflict")).IsEqualTo("thread already has an active writer");
        await Assert.That(result.Value<string>("serverThreadConflict")!).Contains("already has an active Codex turn");
        await Assert.That(result.Value<int>("conflictSdkRuns")).IsEqualTo(0);
        await Assert.That(result.Value<string>("startupFallback")).IsEqualTo("startup fallback");
        await Assert.That(result.Value<int>("sdkRuns")).IsEqualTo(1);
    }

    /// <summary>Finds the source tree containing the bridge fixture.</summary>
    /// <returns>The absolute repository path.</returns>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
