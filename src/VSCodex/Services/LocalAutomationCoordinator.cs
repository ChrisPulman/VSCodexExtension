// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using VSCodex.Core.Automations;
using VSCodex.Infrastructure;

namespace VSCodex.Services;

/// <summary>Serializes short state transactions and retains a separate live lease for each execution.</summary>
/// <param name="path">The shared automation JSON path.</param>
public sealed class LocalAutomationCoordinator(string path) : ILocalAutomationCoordinator
{
    /// <summary>Extracts a Win32 error from an IO exception.</summary>
    private const int NativeErrorMask = 65_535;

    /// <summary>Identifies a sharing violation.</summary>
    private const int SharingViolation = 32;

    /// <summary>Identifies a lock violation.</summary>
    private const int LockViolation = 33;

    /// <summary>Defines the shared state lease suffix.</summary>
    private const string StateLeaseSuffix = ".lease";

    /// <summary>Stores atomic JSON serialization.</summary>
    private readonly JsonFileStore _files = new();

    /// <summary>Reloads current state under the lease and optionally applies one mutation.</summary>
    /// <param name="mutation">The requested mutation, or null to refresh only.</param>
    /// <returns>The current persisted rows, or null if another process owns the lease.</returns>
    public List<LocalAutomation>? TryUpdate(Action<List<LocalAutomation>>? mutation)
    {
        using FileStream? stateLease = TryOpenLease(path + StateLeaseSuffix);
        if (stateLease is null)
        {
            return null;
        }

        List<LocalAutomation> jobs = _files.ReadOrCreate<List<LocalAutomation>>(path);
        if (mutation is not null)
        {
            mutation(jobs);
            _files.Write(path, jobs);
        }

        return jobs;
    }

    /// <summary>Reloads the current shared state without changing any rows.</summary>
    /// <returns>The latest rows, or null when another host owns the state lease.</returns>
    public List<LocalAutomation>? TryUpdate() => TryUpdate(null);

    /// <summary>Claims a latest-state schedule and checkpoints ownership before execution.</summary>
    /// <param name="id">The selected schedule identifier.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <param name="leaseDuration">The timeout plus recovery margin.</param>
    /// <returns>The live execution ownership handle, or null when no claim can be acquired.</returns>
    public LocalAutomationClaim? TryClaim(string id, DateTimeOffset now, TimeSpan leaseDuration)
    {
        using FileStream? stateLease = TryOpenLease(path + StateLeaseSuffix);
        if (stateLease is null)
        {
            return null;
        }

        List<LocalAutomation> jobs = _files.ReadOrCreate<List<LocalAutomation>>(path);
        LocalAutomation? job = jobs.FirstOrDefault(item => item.Id == id);
        if (job is null || !IsDue(job, now))
        {
            return null;
        }

        FileStream? executionLease = TryOpenLease($"{path}.{LeaseSuffix(id)}.run");
        if (executionLease is null)
        {
            return null;
        }

        try
        {
            job.ClaimId = Guid.NewGuid().ToString("N");
            job.ClaimExpires = now.Add(leaseDuration);
            job.NextDue = now.AddMinutes(job.IntervalMinutes);
            _files.Write(path, jobs);
            return new(job, executionLease);
        }
        catch
        {
            executionLease.Dispose();
            throw;
        }
    }

    /// <summary>Merges only execution outcome fields into a surviving owner token.</summary>
    /// <param name="completed">The detached completed execution snapshot.</param>
    /// <returns>False only when the state lease is currently busy.</returns>
    public bool TryComplete(LocalAutomation completed)
    {
        List<LocalAutomation>? result = TryUpdate(jobs =>
        {
            LocalAutomation? latest = jobs.FirstOrDefault(item => item.Id == completed.Id && item.ClaimId == completed.ClaimId);
            if (latest is null)
            {
                return;
            }

            latest.LastCompleted = completed.LastCompleted;
            latest.LastResult = completed.LastResult;
            latest.LastFailed = completed.LastFailed;
            latest.Unread = true;
            latest.ClaimId = string.Empty;
            latest.ClaimExpires = null;
        });
        return result is not null;
    }

    /// <summary>Opens an exclusive lease immediately without retrying a busy owner.</summary>
    /// <param name="leasePath">The lease file path.</param>
    /// <returns>The ownership handle, or null when another process owns it.</returns>
    private static FileStream? TryOpenLease(string leasePath)
    {
        string? directory = Path.GetDirectoryName(leasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        try
        {
            return new(leasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception) when ((exception.HResult & NativeErrorMask) is SharingViolation or LockViolation)
        {
            return null;
        }
    }

    /// <summary>Builds a stable safe lease suffix from an untrusted persisted identifier.</summary>
    /// <param name="id">The schedule identifier.</param>
    /// <returns>The path-safe lease suffix.</returns>
    private static string LeaseSuffix(string id)
    {
        using SHA256 hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(id))).Replace("-", string.Empty);
    }

    /// <summary>Determines whether a schedule is due or an expired owner needs recovery.</summary>
    /// <param name="job">The latest persisted row.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>True when a claim may be attempted.</returns>
    private static bool IsDue(LocalAutomation job, DateTimeOffset now)
    {
        bool hasClaim = !string.IsNullOrEmpty(job.ClaimId);
        bool claimExpired = hasClaim && (!job.ClaimExpires.HasValue || job.ClaimExpires <= now);
        return job.Enabled && job.IntervalMinutes > 0 && (hasClaim ? claimExpired : job.NextDue <= now);
    }
}
