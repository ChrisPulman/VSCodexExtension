// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace VSCodex.Core.Chats;

/// <summary>Describes turn operations supported by a Codex transport.</summary>
public sealed class ChatTransportCapabilities
{
    /// <summary>Initializes a new instance of the <see cref="ChatTransportCapabilities"/> class.</summary>
    /// <param name="canSteer">Whether active turns can receive steering input.</param>
    /// <param name="canResume">Whether durable checkpoints can resume their thread.</param>
    public ChatTransportCapabilities(bool canSteer, bool canResume)
    {
        CanSteer = canSteer;
        CanResume = canResume;
    }

    /// <summary>Gets a value indicating whether the active turn can be steered.</summary>
    public bool CanSteer { get; }

    /// <summary>Gets a value indicating whether a durable checkpoint can resume its thread.</summary>
    public bool CanResume { get; }
}
