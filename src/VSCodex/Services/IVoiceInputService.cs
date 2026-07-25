// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;

namespace VSCodex.Services;

/// <summary>Defines the i Voice Input Service contract.</summary>
public interface IVoiceInputService : IDisposable
{
    /// <summary>Gets the transcript.</summary>
    IObservable<string> Transcript { get; }

    /// <summary>Gets the status.</summary>
    IObservable<string> Status { get; }

    /// <summary>Gets the is Available.</summary>
    bool IsAvailable { get; }

    /// <summary>Gets the is Listening.</summary>
    bool IsListening { get; }

    /// <summary>Starts the operation.</summary>
    void Start();

    /// <summary>Stops the operation.</summary>
    void Stop();
}
