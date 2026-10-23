// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using VSCodex.Core.Models;

namespace VSCodex.Tests;

/// <summary>Verifies bounded immutable solution metadata.</summary>
public sealed class SolutionContextSnapshotTests
{
    /// <summary>The original document path.</summary>
    private const string OriginalDocument = "original.cs";

    /// <summary>The deliberately oversized field length.</summary>
    private const int OversizedLength = 2_000;

    /// <summary>The deliberately oversized row count.</summary>
    private const int OversizedCount = 1_000;

    /// <summary>Verifies immutable bounded metadata.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Snapshot_copies_inputs_and_preserves_enqueue_metadata()
    {
        var projects = new List<string> { "Application | app.csproj | CSharp" };
        var documents = new List<string> { OriginalDocument };
        var snapshot = new SolutionContextSnapshot("original.sln", ("Debug", "Any CPU"), ["Application"], projects, (OriginalDocument, true), documents, ["Unloaded project path unavailable"]);
        projects.Clear();
        documents[0] = "newer.cs";
        await Assert.That(snapshot.Projects.Count).IsEqualTo(1);
        await Assert.That(snapshot.OpenDocuments[0]).IsEqualTo(OriginalDocument);
        await Assert.That(snapshot.Format()).Contains("original.sln");
        await Assert.That(snapshot.Format()).Contains("Dirty: True");
        await Assert.That(snapshot.Format()).Contains("Unloaded project path unavailable");
    }

    /// <summary>Verifies immutable bounded metadata.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task Snapshot_bounds_lists_text_and_formatted_output()
    {
        var rows = Enumerable.Repeat(new string('x', OversizedLength), OversizedCount).ToArray();
        var snapshot = new SolutionContextSnapshot("solution\nspoof", ("Debug", "x64"), rows, rows, ("file.cs", null), rows, rows);
        await Assert.That(snapshot.Projects.Count).IsEqualTo(SolutionContextSnapshot.MaxProjects);
        await Assert.That(snapshot.OpenDocuments.Count).IsEqualTo(SolutionContextSnapshot.MaxDocuments);
        await Assert.That(snapshot.Projects[0].Length).IsEqualTo(SolutionContextSnapshot.MaxTextLength);
        await Assert.That(snapshot.SolutionPath).IsEqualTo("solution spoof");
        await Assert.That(snapshot.Format().Length <= SolutionContextSnapshot.MaxFormattedLength).IsTrue();
    }
}
