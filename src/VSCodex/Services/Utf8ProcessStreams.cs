// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.IO;
using System.Text;

namespace VSCodex.Services;

/// <summary>Creates BOM-free UTF-8 writers for newline-delimited process protocols.</summary>
internal static class Utf8ProcessStreams
{
    /// <summary>Creates a writer that owns and flushes the supplied standard-input pipe.</summary>
    /// <param name="stream">The redirected process input stream.</param>
    /// <returns>A UTF-8 writer without a byte order mark.</returns>
    internal static StreamWriter CreateWriter(Stream stream) => new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
}
