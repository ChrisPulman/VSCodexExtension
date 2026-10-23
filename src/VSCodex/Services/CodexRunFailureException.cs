// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;

namespace VSCodex.Services;

/// <summary>Reports an uncertain or failed dispatched turn that must not be replayed automatically.</summary>
public sealed class CodexRunFailureException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="CodexRunFailureException"/> class.</summary>
    public CodexRunFailureException()
        : base("Codex could not finish this request. Review any changes before retrying.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CodexRunFailureException"/> class.</summary>
    /// <param name="message">The failure explanation.</param>
    public CodexRunFailureException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CodexRunFailureException"/> class.</summary>
    /// <param name="message">The failure explanation.</param>
    /// <param name="innerException">The execution failure.</param>
    public CodexRunFailureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CodexRunFailureException"/> class.</summary>
    /// <param name="innerException">The transport or execution failure.</param>
    public CodexRunFailureException(Exception innerException)
        : base($"Codex could not finish this request. Review any changes before retrying. {innerException.Message}", innerException)
    {
    }
}
