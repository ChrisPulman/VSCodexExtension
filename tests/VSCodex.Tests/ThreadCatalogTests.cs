// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace VSCodex.Tests;

/// <summary>Exercises real thread adapter code rather than duplicating its implementation.</summary>
public sealed class ThreadCatalogTests
{
    /// <summary>Bounds execution of the JavaScript protocol fixture.</summary>
    private const int ProbeTimeoutSeconds = 10;

    /// <summary>Counts the unique threads after catalog deduplication.</summary>
    private const int ExpectedThreadCount = 2;

    /// <summary>Names the JSON-RPC parameters member.</summary>
    private const string ParametersProperty = "params";

    /// <summary>Names the captured mutation/read operations.</summary>
    private const string OperationsProperty = "operations";

    /// <summary>Verifies pagination, hydration, fork, archive and non-replayed failures.</summary>
    /// <returns>A task that completes after the protocol probe.</returns>
    [Test]
    public async Task ThreadManagementPreservesProtocolAndDoesNotReplayMutations()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props")))
        {
            directory = directory.Parent;
        }

        string root = directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(root, "tests", "bridge-thread-catalog-probe.mjs"));
        start.ArgumentList.Add(Path.Combine(root, "src", "VSCodex", "Resources", "codex-bridge.mjs"));
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(ProbeTimeoutSeconds));
        await process.WaitForExitAsync(timeout.Token);
        await Assert.That(process.ExitCode).IsEqualTo(0).Because(await errors);
        JObject result = JObject.Parse(await output);
        await Assert.That(result["catalog"]!["data"]!.Count()).IsEqualTo(ExpectedThreadCount);
        await Assert.That(result["catalog"]!["data"]![0]!.Value<string>("preview")).IsEqualTo("new");
        await Assert.That(result["catalogCalls"]![1]![ParametersProperty]!.Value<string>("cursor")).IsEqualTo("page2");
        await Assert.That(result["catalogCalls"]![1]![ParametersProperty]!.Value<bool>("archived")).IsTrue();
        await Assert.That(result[OperationsProperty]![0]![ParametersProperty]!.Value<bool>("includeTurns")).IsTrue();
        await Assert.That(result["fork"]!["thread"]!.Value<string>("id")).IsEqualTo("forked");
        await Assert.That(result[OperationsProperty]![2]!.Value<string>("method")).IsEqualTo("thread/archive");
        await Assert.That(result[OperationsProperty]![3]!.Value<string>("method")).IsEqualTo("thread/unarchive");
        await Assert.That(result.Value<int>("mutationAttempts")).IsEqualTo(1);
        await Assert.That(result.Value<string>("failure")).IsEqualTo("archive denied");
        await Assert.That(result.Value<string>("paginationFailure")).IsEqualTo("Codex thread/list repeated a pagination cursor.");
        await Assert.That(result.Value<string>("missingIdFailure")).IsEqualTo("A Codex thread identifier is required.");
    }
}
