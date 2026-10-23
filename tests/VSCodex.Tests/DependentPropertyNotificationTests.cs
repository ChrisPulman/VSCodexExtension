// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using VSCodex.Models;

namespace VSCodex.Tests;

/// <summary>Checks the actual reactive models notify WPF's computed binding dependencies.</summary>
public sealed class DependentPropertyNotificationTests
{
    /// <summary>Ready and missing setup states refresh the available actions and button caption.</summary>
    /// <returns>A task that completes after dependency assertions.</returns>
    [Test]
    public async Task PrerequisiteTransitionsNotifyComputedActions()
    {
        PrerequisiteStatus status = new() { InstallCommand = "install", State = PrerequisiteState.Ready };
        List<string?> names = new();
        status.PropertyChanged += (_, change) => names.Add(change.PropertyName);
        status.State = PrerequisiteState.Missing;
        await Assert.That(status.CanCopyCommand).IsTrue();
        await Assert.That(status.CanUpdate).IsTrue();
        await Assert.That(names).Contains(nameof(PrerequisiteStatus.CanCopyCommand));
        await Assert.That(names).Contains(nameof(PrerequisiteStatus.CanUpdate));
        await Assert.That(names).Contains(nameof(PrerequisiteStatus.ActionButtonText));
        names.Clear();
        status.State = PrerequisiteState.Ready;
        await Assert.That(status.CanUpdate).IsFalse();
        await Assert.That(names).Contains(nameof(PrerequisiteStatus.CanUpdate));
    }

    /// <summary>Command and status edits update dependent action bindings without replacing the row.</summary>
    /// <returns>A task that completes after dependency assertions.</returns>
    [Test]
    public async Task PrerequisiteEditsRefreshActionAndCaption()
    {
        PrerequisiteStatus status = new() { State = PrerequisiteState.Warning };
        List<string?> names = new();
        status.PropertyChanged += (_, change) => names.Add(change.PropertyName);
        status.UpdateCommand = "upgrade";
        await Assert.That(status.ActionCommand).IsEqualTo("upgrade");
        await Assert.That(status.CanUpdate).IsTrue();
        await Assert.That(names).Contains(nameof(PrerequisiteStatus.ActionCommand));
        await Assert.That(names).Contains(nameof(PrerequisiteStatus.CanCopyCommand));
        names.Clear();
        status.Status = "not found";
        await Assert.That(status.ActionButtonText).IsEqualTo("Install");
        await Assert.That(names).Contains(nameof(PrerequisiteStatus.ActionButtonText));
    }

    /// <summary>Required servers cannot be disabled and endpoint edits refresh the display binding.</summary>
    /// <returns>A task that completes after dependency assertions.</returns>
    [Test]
    public async Task RequiredServerAndEndpointChangesNotifyComputedBindings()
    {
        McpServerDefinition server = new() { IsEnabled = false };
        List<string?> names = new();
        server.PropertyChanged += (_, change) => names.Add(change.PropertyName);
        server.IsRequired = true;
        await Assert.That(server.IsEnabled).IsTrue();
        await Assert.That(server.CanDisable).IsFalse();
        await Assert.That(server.CanRemove).IsFalse();
        await Assert.That(names).Contains(nameof(McpServerDefinition.CanDisable));
        await Assert.That(names).Contains(nameof(McpServerDefinition.CanRemove));
        names.Clear();
        server.TransportType = "url";
        server.Url = "https://localhost/mcp";
        await Assert.That(server.EndpointSummary).IsEqualTo(server.Url);
        await Assert.That(names).Contains(nameof(McpServerDefinition.EndpointSummary));
    }
}
