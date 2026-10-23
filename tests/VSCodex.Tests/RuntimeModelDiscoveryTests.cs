// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using TUnit.Assertions.Enums;
using VSCodex.Core.Models;

namespace VSCodex.Tests;

/// <summary>Verifies account model discovery and saved model compatibility.</summary>
public sealed class RuntimeModelDiscoveryTests
{
    /// <summary>Available runtime choice.</summary>
    private const string AvailableModel = "available-model";

    /// <summary>Previously shipped choice.</summary>
    private const string RetiredModel = "gpt-5.5";

    /// <summary>Future advertised effort.</summary>
    private const string FutureEffort = "future-effort";

    /// <summary>Maximum probe runtime.</summary>
    private const int ProbeTimeoutSeconds = 10;

    /// <summary>Verifies real bridge pagination, error handling, and advertised effort normalization.</summary>
    /// <returns>A task that completes after running the bridge fixture.</returns>
    [Test]
    public async Task Bridge_discovers_all_pages_and_uses_advertised_reasoning()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props")))
        {
            directory = directory.Parent;
        }

        string root = directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(root, "tests", "fixtures", "bridge-models-probe.mjs"));
        start.ArgumentList.Add(Path.Combine(root, "src", "VSCodex", "Resources", "codex-bridge.mjs"));
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(ProbeTimeoutSeconds));
        await process.WaitForExitAsync(timeout.Token);
        await Assert.That(process.ExitCode).IsEqualTo(0).Because(await errors);
        JObject result = JObject.Parse(await output);
        await Assert.That(result[nameof(result)]!["data"]!.Select(model => model.Value<string>("model")!))
            .IsEquivalentTo(["future-model", "second-model"], CollectionOrdering.Matching);
        await Assert.That(result["requests"]![0]!.Value<string>("method")).IsEqualTo("model/list");
        await Assert.That(result["requests"]![1]!["params"]!.Value<string>("cursor")).IsEqualTo("page-2");
        await Assert.That(result["efforts"]!.Values<string>().Select(effort => effort!)).IsEquivalentTo(["ultra", "high", "none"], CollectionOrdering.Matching);
        await Assert.That(result.Value<string>("repeatedCursor")).Contains("repeated a pagination cursor");
        await Assert.That(result.Value<string>("rpcError")).IsEqualTo("model access denied");
    }

    /// <summary>Verifies migration of retired shipped choices while preserving explicit custom selections.</summary>
    /// <returns>A task that completes after checking selection behavior.</returns>
    [Test]
    public async Task Runtime_default_replaces_only_retired_shipped_choices()
    {
        CodexRuntimeModel[] models = [new() { Model = "future-default", IsDefault = true }, new() { Model = AvailableModel }];
        await Assert.That(CodexRuntimeModel.ResolveSelection(RetiredModel, models, [])).IsEqualTo("future-default");
        await Assert.That(CodexRuntimeModel.ResolveSelection(RetiredModel, models, [RetiredModel])).IsEqualTo(RetiredModel);
        await Assert.That(CodexRuntimeModel.ResolveSelection("custom-model", models, [])).IsEqualTo("custom-model");
        await Assert.That(CodexRuntimeModel.ResolveSelection(AvailableModel, models, [])).IsEqualTo(AvailableModel);
        await Assert.That(CodexRuntimeModel.ResolveSelection(RetiredModel, [], [])).IsEqualTo(RetiredModel);
    }

    /// <summary>Verifies future effort values and runtime defaults without hard-coded model names.</summary>
    /// <returns>A task that completes after capability checks.</returns>
    [Test]
    public async Task Advertised_efforts_and_default_are_authoritative()
    {
        var model = new CodexRuntimeModel { Model = "future-model", DefaultReasoningEffort = "high", ReasoningEfforts = ["none", "high", FutureEffort] };
        await Assert.That(model.ResolveReasoningEffort(FutureEffort)).IsEqualTo(FutureEffort);
        await Assert.That(model.ResolveReasoningEffort("medium")).IsEqualTo("high");
        await Assert.That(model.ResolveReasoningEffort(null)).IsEqualTo("high");
    }
}
