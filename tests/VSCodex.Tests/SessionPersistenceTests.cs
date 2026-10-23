// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Newtonsoft.Json;
using VSCodex.Infrastructure;

namespace VSCodex.Tests;

/// <summary>Verifies transactional persistence for history and local display metadata.</summary>
public sealed class SessionPersistenceTests
{
    /// <summary>Verifies an unsuccessful serialization cannot truncate saved history.</summary>
    /// <returns>A task that completes after persistence assertions.</returns>
    [Test]
    public async Task FailedSerializationPreservesPreviouslySavedHistory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"VSCodex-persistence-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "session.json");
        try
        {
            JsonFileStore store = new();
            store.Write(path, new { Title = "Preserved", IsPinned = true });
            string original = File.ReadAllText(path);
            bool failed = false;
            try
            {
                store.Write(path, new ThrowingDocument());
            }
            catch (JsonSerializationException)
            {
                failed = true;
            }

            await Assert.That(failed).IsTrue();
            await Assert.That(File.ReadAllText(path)).IsEqualTo(original);
            await Assert.That(Directory.EnumerateFiles(directory).Count()).IsEqualTo(1);
            store.Write(path, new { Title = "Updated", IsPinned = false });
            await Assert.That(File.ReadAllText(path)).Contains("Updated");
            await Assert.That(Directory.EnumerateFiles(directory).Count()).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Provides a deterministic serialization failure.</summary>
    private sealed class ThrowingDocument
    {
        /// <summary>Gets a value that cannot be serialized.</summary>
        public string Value => throw new InvalidOperationException("Simulated serialization failure.");
    }
}
