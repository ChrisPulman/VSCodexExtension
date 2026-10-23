// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Owns local checkouts and parks archives without deleting user data.</summary>
public sealed class ManagedWorktreeService
{
    /// <summary>Names the worktree Git/storage component.</summary>
    private const string WorktreeCommand = "worktree";

    /// <summary>Names the active Git/storage component.</summary>
    private const string ActiveDirectory = "active";

    /// <summary>Names the rev-parse Git/storage component.</summary>
    private const string RevisionCommand = "rev-parse";

    /// <summary>Names the index Git/storage component.</summary>
    private const string IndexFile = "index";

    /// <summary>Names the archive Git/storage component.</summary>
    private const string ArchiveDirectory = "archive";

    /// <summary>Names the registry.json Git/storage component.</summary>
    private const string RegistryFile = "registry.json";

    /// <summary>Names the manifest.json Git/storage component.</summary>
    private const string ManifestFile = "manifest.json";

    /// <summary>Names the --git-path Git/storage component.</summary>
    private const string GitPathOption = "--git-path";

    /// <summary>Serializes registry operations in this extension process.</summary>
    private static readonly object RegistryGate = new();

    /// <summary>Stores the dedicated managed storage directory.</summary>
    private readonly string _storageRoot;

    /// <summary>Initializes a new instance of the <see cref="ManagedWorktreeService"/> class.</summary>
    /// <param name="storageRoot">The dedicated extension storage directory.</param>
    public ManagedWorktreeService(string storageRoot) => _storageRoot = System.IO.Path.GetFullPath(storageRoot);

    /// <summary>Lists registered Git checkouts together with durable extension metadata.</summary>
    /// <param name="workspace">The current workspace.</param>
    /// <returns>The repository's checkout records.</returns>
    public IReadOnlyList<ManagedWorktree> List(string workspace)
    {
        lock (RegistryGate)
        {
            using FileStream lease = AcquireLease();
            return ListCore(workspace);
        }
    }

    /// <summary>Creates an owned checkout on a new user-named branch.</summary>
    /// <param name="workspace">The source workspace.</param>
    /// <param name="branch">A new branch with the CP_ prefix.</param>
    /// <returns>The created checkout.</returns>
    public ManagedWorktree Create(string workspace, string branch)
    {
        lock (RegistryGate)
        {
            using FileStream lease = AcquireLease();
            if (string.IsNullOrWhiteSpace(branch) || !branch.StartsWith("CP_", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Enter a new branch beginning with CP_.");
            }

            _ = Git(workspace, "check-ref-format", "--branch", branch);
            string id = Guid.NewGuid().ToString("N");
            string path = System.IO.Path.Combine(_storageRoot, ActiveDirectory, id);
            _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            ManagedWorktree item = new() { Id = id, Repository = CommonDirectory(workspace), Path = path, OriginalPath = path, Branch = branch, IsManaged = true };
            List<ManagedWorktree> records = Load();
            records.Add(item);
            Save(records); // Record ownership before Git can create a checkout.
            _ = Git(workspace, WorktreeCommand, "add", "-b", branch, path, "HEAD");
            return item;
        }
    }

    /// <summary>Moves an owned checkout into or out of the archive, retaining byte-verified recovery data.</summary>
    /// <param name="workspace">The active workspace, which cannot itself be archived.</param>
    /// <param name="id">The managed checkout identifier.</param>
    /// <returns>The updated record.</returns>
    public ManagedWorktree ToggleArchive(string workspace, string id)
    {
        lock (RegistryGate)
        {
            using FileStream lease = AcquireLease();
            _ = ListCore(workspace);
            List<ManagedWorktree> records = Load();
            ManagedWorktree item = OwnedRecord(workspace, id, records);
            ValidateMovable(workspace, item);
            string destination = item.IsArchived ? item.OriginalPath : System.IO.Path.Combine(_storageRoot, ArchiveDirectory, item.Id);
            if (Directory.Exists(destination) || File.Exists(destination))
            {
                throw new InvalidOperationException("The destination already exists; the checkout has not been moved.");
            }

            string snapshot = CreateSnapshot(item);
            item.SnapshotPath = snapshot;
            item.PendingPath = destination;
            Save(records);
            _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
            EnsureUnlinked(System.IO.Path.GetDirectoryName(destination)!);
            _ = Git(workspace, WorktreeCommand, "move", item.Path, destination);
            item.Path = destination;
            item.IsArchived = !item.IsArchived;
            item.PendingPath = string.Empty;
            Save(records); // Persist the actual location even if post-move verification reports a concurrent edit.
            VerifySnapshot(item.Path, snapshot);
            return item;
        }
    }

    /// <summary>Toggles archive protection for an owned checkout.</summary>
    /// <param name="workspace">The source workspace.</param>
    /// <param name="id">The managed checkout identifier.</param>
    public void TogglePin(string workspace, string id)
    {
        lock (RegistryGate)
        {
            using FileStream lease = AcquireLease();
            List<ManagedWorktree> records = Load();
            ManagedWorktree item = OwnedRecord(workspace, id, records);
            item.IsPinned = !item.IsPinned;
            Save(records);
        }
    }

    /// <summary>Runs Git with separately quoted arguments and no command shell.</summary>
    /// <param name="workspace">The working directory.</param>
    /// <param name="arguments">The individual arguments.</param>
    /// <returns>The standard output.</returns>
    internal static string Git(string workspace, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = workspace,
            Arguments = string.Join(" ", arguments.Select(Quote)),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Git could not be started.");
        StringBuilder output = new();
        StringBuilder errors = new();
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is null)
            {
                return;
            }

            _ = output.AppendLine(args.Data);
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is null)
            {
                return;
            }

            _ = errors.AppendLine(args.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        string detail = errors.ToString();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Git worktree operation failed: {detail.Trim()}");
        }

        return output.ToString().Replace("\r\n", "\n").TrimEnd('\n');
    }

    /// <summary>Quotes an argument using Windows process escaping rules.</summary>
    /// <param name="value">The argument.</param>
    /// <returns>The quoted argument.</returns>
    private static string Quote(string value)
    {
        if (value.IndexOfAny(['"', '\r', '\n', '\0']) >= 0)
        {
            throw new ArgumentException("Git arguments cannot contain quotes or control line characters.", nameof(value));
        }

        string trimmed = value.TrimEnd('\\');
        string trailingSlashes = new('\\', value.Length - trimmed.Length);
        return $"\"{trimmed}{trailingSlashes}{trailingSlashes}\"";
    }

    /// <summary>Resolves the repository identity shared by linked worktrees.</summary>
    /// <param name="workspace">The working directory.</param>
    /// <returns>The canonical common Git directory.</returns>
    private static string CommonDirectory(string workspace)
    {
        string common = Git(workspace, RevisionCommand, "--git-common-dir");
        return System.IO.Path.GetFullPath(System.IO.Path.IsPathRooted(common) ? common : System.IO.Path.Combine(workspace, common));
    }

    /// <summary>Compares canonical local paths.</summary>
    /// <param name="left">The first path.</param>
    /// <param name="right">The second path.</param>
    /// <returns>Whether both paths identify the same location.</returns>
    private static bool Same(string left, string right) => string.Equals(
        System.IO.Path.GetFullPath(left).TrimEnd('\\', '/'),
        System.IO.Path.GetFullPath(right).TrimEnd('\\', '/'),
        StringComparison.OrdinalIgnoreCase);

    /// <summary>Matches an owned location or an interrupted move destination.</summary>
    /// <param name="item">The record.</param>
    /// <param name="path">The Git-reported directory.</param>
    /// <returns>Whether the directory belongs to the record.</returns>
    private static bool MatchesLocation(ManagedWorktree item, string path) => Same(item.Path, path)
        || (!string.IsNullOrEmpty(item.PendingPath) && Same(item.PendingPath, path));

    /// <summary>Enumerates files while refusing links and embedded repositories.</summary>
    /// <param name="root">The directory being scanned.</param>
    /// <param name="linkedRoot">Whether this is the linked checkout root.</param>
    /// <returns>The safe regular files.</returns>
    private static IEnumerable<string> SafeFiles(string root, bool linkedRoot = true)
    {
        foreach (string entry in Directory.EnumerateFileSystemEntries(root))
        {
            if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException($"Recovery cannot be guaranteed for linked files or directories: {entry}");
            }

            if (IsGitMetadata(entry, linkedRoot))
            {
                continue;
            }

            if (Directory.Exists(entry))
            {
                foreach (string file in SafeFiles(entry, false))
                {
                    yield return file;
                }
            }
            else
            {
                yield return entry;
            }
        }
    }

    /// <summary>Identifies root Git metadata and refuses embedded repositories.</summary>
    /// <param name="entry">The filesystem entry.</param>
    /// <param name="linkedRoot">Whether this is the checkout root.</param>
    /// <returns>Whether the entry is the root metadata file.</returns>
    private static bool IsGitMetadata(string entry, bool linkedRoot)
    {
        if (System.IO.Path.GetFileName(entry) != ".git")
        {
            return false;
        }

        if (linkedRoot && !Directory.Exists(entry))
        {
            return true;
        }

        throw new InvalidOperationException($"Embedded repositories cannot be archived: {entry}");
    }

    /// <summary>Hashes a regular file without text decoding.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The content hash.</returns>
    private static string Hash(string path)
    {
        using var algorithm = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToBase64String(algorithm.ComputeHash(stream));
    }

    /// <summary>Checks every source byte against the retained snapshot.</summary>
    /// <param name="checkout">The checkout directory.</param>
    /// <param name="snapshot">The snapshot directory.</param>
    private static void VerifySnapshot(string checkout, string snapshot)
    {
        Dictionary<string, string> expected = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(System.IO.Path.Combine(snapshot, ManifestFile)))!;
        string[] files = SafeFiles(checkout).ToArray();
        if (files.Length != expected.Count || files.Any(file => !expected.TryGetValue(file.Substring(checkout.Length), out string? hash) || hash != Hash(file)))
        {
            throw new IOException($"Checkout changed during archival; retained recovery snapshot: {snapshot}");
        }

        string index = Git(checkout, RevisionCommand, GitPathOption, IndexFile);
        index = System.IO.Path.IsPathRooted(index) ? index : System.IO.Path.Combine(checkout, index);
        if (Hash(index) == Hash(System.IO.Path.Combine(snapshot, IndexFile)))
        {
            return;
        }

        throw new IOException($"Git staging index changed during archival; retained recovery snapshot: {snapshot}");
    }

    /// <summary>Refuses link traversal through a directory and its ancestors.</summary>
    /// <param name="path">The directory to validate.</param>
    private static void EnsureUnlinked(string path)
    {
        DirectoryInfo? directory = new(path);
        while (directory is not null)
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Managed worktrees cannot use linked directories.");
            }

            directory = directory.Parent;
        }
    }

    /// <summary>Lists and recovers interrupted move metadata under the registry lease.</summary>
    /// <param name="workspace">The current workspace.</param>
    /// <returns>The repository checkouts.</returns>
    private List<ManagedWorktree> ListCore(string workspace)
    {
        string repository = CommonDirectory(workspace);
        List<ManagedWorktree> owned = Load();
        List<ManagedWorktree> result = new();
        foreach (string block in Git(workspace, WorktreeCommand, "list", "--porcelain").Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            string[] lines = block.Split('\n');
            string path = lines.First(x => x.StartsWith("worktree ", StringComparison.Ordinal)).Substring("worktree ".Length);
            ManagedWorktree item = owned.FirstOrDefault(x => Same(x.Repository, repository) && MatchesLocation(x, path))
                ?? new ManagedWorktree { Path = path, Repository = repository };
            if (!string.IsNullOrEmpty(item.PendingPath))
            {
                item.Path = path;
                item.IsArchived = Same(path, System.IO.Path.Combine(_storageRoot, ArchiveDirectory, item.Id));
                item.PendingPath = string.Empty;
                Save(owned);
            }

            item.Branch = lines.FirstOrDefault(x => x.StartsWith("branch ", StringComparison.Ordinal))?.Substring("branch refs/heads/".Length) ?? "detached HEAD";
            item.IsLocked = lines.Any(x => x.StartsWith("locked", StringComparison.Ordinal));
            result.Add(item);
        }

        return result;
    }

    /// <summary>Loads the durable ownership registry without hiding corruption.</summary>
    /// <returns>The owned records.</returns>
    private List<ManagedWorktree> Load()
    {
        string path = System.IO.Path.Combine(_storageRoot, RegistryFile);
        return File.Exists(path)
            ? JsonConvert.DeserializeObject<List<ManagedWorktree>>(File.ReadAllText(path)) ?? throw new IOException("Worktree registry is empty or corrupt.")
            : new List<ManagedWorktree>();
    }

    /// <summary>Atomically replaces the durable registry.</summary>
    /// <param name="records">The owned records.</param>
    private void Save(List<ManagedWorktree> records)
    {
        _ = Directory.CreateDirectory(_storageRoot);
        string path = System.IO.Path.Combine(_storageRoot, RegistryFile);
        string temporary = $"{path}.{Guid.NewGuid():N}";
        File.WriteAllText(temporary, JsonConvert.SerializeObject(records, Formatting.Indented));
        if (File.Exists(path))
        {
            File.Replace(temporary, path, $"{path}.previous");
        }
        else
        {
            File.Move(temporary, path);
        }
    }

    /// <summary>Resolves ownership and validates the dedicated storage boundary.</summary>
    /// <param name="workspace">The active workspace.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="records">The registry records.</param>
    /// <returns>The owned record.</returns>
    private ManagedWorktree OwnedRecord(string workspace, string id, List<ManagedWorktree> records)
    {
        ManagedWorktree item = records.Single(x => x.Id == id && x.IsManaged && Same(x.Repository, CommonDirectory(workspace)));
        string expected = System.IO.Path.Combine(_storageRoot, item.IsArchived ? ArchiveDirectory : ActiveDirectory, item.Id);
        if (!Guid.TryParseExact(item.Id, "N", out _) || !Same(item.Path, expected) || !Same(item.OriginalPath, System.IO.Path.Combine(_storageRoot, ActiveDirectory, item.Id)))
        {
            throw new InvalidOperationException("Worktree ownership or storage boundary is invalid.");
        }

        return item;
    }

    /// <summary>Refuses primary, active, pinned, locked, shared and submodule checkouts.</summary>
    /// <param name="workspace">The active workspace.</param>
    /// <param name="item">The owned checkout.</param>
    private void ValidateMovable(string workspace, ManagedWorktree item)
    {
        EnsureUnlinked(item.Path);
        string active = Git(workspace, RevisionCommand, "--show-toplevel");
        ManagedWorktree registered = ListCore(workspace).Single(x => Same(x.Path, item.Path));
        if (Same(active, item.Path) || item.IsPinned || registered.IsLocked || !File.Exists(System.IO.Path.Combine(item.Path, ".git")))
        {
            throw new InvalidOperationException("Active, primary, pinned or Git-locked/shared checkouts cannot be moved.");
        }

        if (!Git(item.Path, "ls-files", "--stage").Split('\n').Any(line => line.StartsWith("160000 ", StringComparison.Ordinal)))
        {
            return;
        }

        throw new InvalidOperationException("Worktrees containing submodules cannot be archived safely.");
    }

    /// <summary>Obtains exclusive cross-process access and rejects linked storage ancestry.</summary>
    /// <returns>The lease, released when disposed.</returns>
    private FileStream AcquireLease()
    {
        _ = Directory.CreateDirectory(_storageRoot);
        EnsureUnlinked(_storageRoot);
        return new(System.IO.Path.Combine(_storageRoot, "registry.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    /// <summary>Copies all regular working files and the staging index, then verifies their hashes.</summary>
    /// <param name="item">The owned checkout.</param>
    /// <returns>The retained recovery directory.</returns>
    private string CreateSnapshot(ManagedWorktree item)
    {
        string snapshot = System.IO.Path.Combine(_storageRoot, "snapshots", item.Id, Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(snapshot);
        Dictionary<string, string> hashes = new(StringComparer.OrdinalIgnoreCase);
        foreach (string file in SafeFiles(item.Path))
        {
            string relative = file.Substring(item.Path.Length);
            string copy = $"{snapshot}{System.IO.Path.DirectorySeparatorChar}files{relative}";
            _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(copy)!);
            File.Copy(file, copy);
            hashes.Add(relative, Hash(copy));
        }

        string index = Git(item.Path, RevisionCommand, GitPathOption, IndexFile);
        File.Copy(System.IO.Path.IsPathRooted(index) ? index : System.IO.Path.Combine(item.Path, index), System.IO.Path.Combine(snapshot, IndexFile));
        File.WriteAllText(System.IO.Path.Combine(snapshot, ManifestFile), JsonConvert.SerializeObject(hashes, Formatting.Indented));
        File.WriteAllText(System.IO.Path.Combine(snapshot, "head.txt"), Git(item.Path, RevisionCommand, "HEAD"));
        VerifySnapshot(item.Path, snapshot);
        return snapshot;
    }
}
