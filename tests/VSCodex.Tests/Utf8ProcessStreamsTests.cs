// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Text;
using VSCodex.Services;

namespace VSCodex.Tests;

/// <summary>Verifies Unicode process input independently of the Windows ANSI code page.</summary>
public sealed class Utf8ProcessStreamsTests
{
    /// <summary>Verifies protocol bytes remain UTF-8 and contain no BOM before JSON.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task Writer_preserves_unicode_and_flushes_without_a_bom()
    {
        const string payload = "{\"prompt\":\"ä ö ü Ä Ö Ü ß „Test“ € – geändert für Überprüfung 日本語 😀\"}";
        using var stream = new MemoryStream();
        using var writer = Utf8ProcessStreams.CreateWriter(stream);
        await writer.WriteLineAsync(payload);
        var expected = new UTF8Encoding(false).GetBytes(payload + writer.NewLine);
        await Assert.That(stream.ToArray()).IsEquivalentTo(expected);
        await Assert.That(Encoding.UTF8.GetString(stream.ToArray())).IsEqualTo(payload + writer.NewLine);
        await Assert.That(stream.ToArray()[0]).IsEqualTo((byte)'{');
    }
}
