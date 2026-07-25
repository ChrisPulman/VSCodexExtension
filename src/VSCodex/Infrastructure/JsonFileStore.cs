// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.IO;
using Newtonsoft.Json;

namespace VSCodex.Infrastructure;

/// <summary>Provides the json File Store implementation.</summary>
public sealed class JsonFileStore
{
    /// <summary>Stores the settings.</summary>
    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        TypeNameHandling = TypeNameHandling.None,
        NullValueHandling = NullValueHandling.Ignore,
    };

    /// <summary>Reads or Create.</summary>
    /// <typeparam name="T">The t type.</typeparam>
    /// <param name="path">The path.</param>
    /// <param name="fallbackFactories">Optional factories used when no serialized value can be obtained.</param>
    /// <returns>The read Or Create result.</returns>
    public T ReadOrCreate<T>(string path, params Func<T>[] fallbackFactories)
        where T : new()
    {
        var createFallback = fallbackFactories.Length > 0 ? fallbackFactories[0] : static () => new T();
        if (!File.Exists(path))
        {
            var created = createFallback();
            Write(path, created);
            return created;
        }

        return JsonConvert.DeserializeObject<T>(File.ReadAllText(path), Settings) ?? createFallback();
    }

    /// <summary>Writes the operation.</summary>
    /// <typeparam name="T">The t type.</typeparam>
    /// <param name="path">The path.</param>
    /// <param name="value">The value.</param>
    public void Write<T>(string path, T value)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            _ = Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, JsonConvert.SerializeObject(value, Settings));
    }
}
