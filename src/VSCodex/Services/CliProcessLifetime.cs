// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VSCodex.Services;

/// <summary>Owns a Windows job so CLI and MCP child processes end together.</summary>
internal sealed class CliProcessLifetime : IDisposable
{
    /// <summary>Defines the extended job information class.</summary>
    private const int ExtendedInformationClass = 9;

    /// <summary>Defines the kill-on-close limit flag.</summary>
    private const int KillOnClose = 0x2000;

    /// <summary>Defines the limit flags offset in JOBOBJECT_BASIC_LIMIT_INFORMATION.</summary>
    private const int FlagsOffset = 16;

    /// <summary>Defines the native extended information size on AMD64.</summary>
    private const int ExtendedSize64 = 144;

    /// <summary>Defines the native extended information size on x86.</summary>
    private const int ExtendedSize32 = 112;

    /// <summary>Defines the size of a 64-bit pointer.</summary>
    private const int PointerSize64 = 8;

    /// <summary>Stores the owned native job handle.</summary>
    private readonly SafeFileHandle _job;

    /// <summary>Initializes a new instance of the <see cref="CliProcessLifetime"/> class.</summary>
    /// <param name="job">The owned job.</param>
    private CliProcessLifetime(SafeFileHandle job) => _job = job;

    /// <summary>Terminates all remaining owned processes and releases the job.</summary>
    public void Dispose() => _job.Dispose();

    /// <summary>Assigns a launched CLI to a kill-on-close job.</summary>
    /// <param name="process">The owned CLI process.</param>
    /// <returns>The process-tree lifetime.</returns>
    internal static CliProcessLifetime Attach(Process process)
    {
        SafeFileHandle job = CreateJobObject(IntPtr.Zero, null);
        if (job.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            process.Kill();
            throw new Win32Exception(error, "Cannot create the Codex CLI process job.");
        }

        if (ConfigureJob(job) && AssignProcessToJobObject(job, process.Handle))
        {
            return new(job);
        }

        int failure = Marshal.GetLastWin32Error();
        job.Dispose();
        if (!process.HasExited)
        {
            process.Kill();
        }

        throw new Win32Exception(failure, "Cannot own the Codex CLI process tree.");
    }

    /// <summary>Configures the documented native extended-limit buffer.</summary>
    /// <param name="job">The job handle.</param>
    /// <returns>Whether configuration succeeded.</returns>
    private static bool ConfigureJob(SafeFileHandle job)
    {
        int size = IntPtr.Size == PointerSize64 ? ExtendedSize64 : ExtendedSize32;
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.Copy(new byte[size], 0, buffer, size);
            Marshal.WriteInt32(buffer, FlagsOffset, KillOnClose);
            return SetInformationJobObject(job, ExtendedInformationClass, buffer, (uint)size);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>Creates a native job.</summary>
    /// <param name="attributes">Security attributes.</param>
    /// <param name="name">Optional job name.</param>
    /// <returns>The job handle.</returns>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);

    /// <summary>Sets job information.</summary>
    /// <param name="job">The job.</param>
    /// <param name="informationClass">Information type.</param>
    /// <param name="information">Native buffer.</param>
    /// <param name="size">Buffer length.</param>
    /// <returns>Whether the operation succeeded.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass, IntPtr information, uint size);

    /// <summary>Assigns a process to the job.</summary>
    /// <param name="job">The job.</param>
    /// <param name="process">The process.</param>
    /// <returns>Whether assignment succeeded.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
}
