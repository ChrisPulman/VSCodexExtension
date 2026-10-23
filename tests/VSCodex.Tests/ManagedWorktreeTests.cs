// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Newtonsoft.Json;
using VSCodex.Models;
using VSCodex.Services;

namespace VSCodex.Tests;

/// <summary>Exercises managed worktrees against isolated real Git repositories.</summary>
public sealed class ManagedWorktreeTests
{
    /// <summary>Names the tracked.txt fixture component.</summary>
    private const string TrackedFile = "tracked.txt";

    /// <summary>Names the ignored.bin fixture component.</summary>
    private const string IgnoredFile = "ignored.bin";

    /// <summary>Names the worktree fixture component.</summary>
    private const string WorktreeCommand = "worktree";

    /// <summary>Names the committed content fixture component.</summary>
    private const string CommittedContent = "committed content";

    /// <summary>Verifies archive and restore preserve dirty files, ignored files and staging bytes.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task ArchiveRestorePreservesIndexAndAllWorkingBytes()
    {
        using var repository = new TemporaryRepository();
        ManagedWorktree item = repository.Service.Create(repository.Path, "CP_preserve");
        string tracked = System.IO.Path.Combine(item.Path, TrackedFile);
        File.WriteAllText(tracked, "staged version");
        _ = ManagedWorktreeService.Git(item.Path, "add", TrackedFile);
        File.WriteAllText(tracked, "unstaged version");
        File.WriteAllText(System.IO.Path.Combine(item.Path, "untracked.txt"), "untracked content");
        File.WriteAllText(System.IO.Path.Combine(item.Path, IgnoredFile), "ignored content");
        string status = ManagedWorktreeService.Git(item.Path, "status", "--porcelain");
        string staged = ManagedWorktreeService.Git(item.Path, "show", ":tracked.txt");
        string original = item.Path;
        ManagedWorktree archived = repository.Service.ToggleArchive(repository.Path, item.Id);
        await Assert.That(archived.IsArchived).IsTrue();
        await Assert.That(Directory.Exists(original)).IsFalse();
        await Assert.That(File.Exists(System.IO.Path.Combine(archived.SnapshotPath, "manifest.json"))).IsTrue();
        ManagedWorktree restored = repository.Service.ToggleArchive(repository.Path, item.Id);
        await Assert.That(restored.Path).IsEqualTo(original);
        await Assert.That(restored.IsArchived).IsFalse();
        await Assert.That(ManagedWorktreeService.Git(restored.Path, "status", "--porcelain")).IsEqualTo(status);
        await Assert.That(ManagedWorktreeService.Git(restored.Path, "show", ":tracked.txt")).IsEqualTo(staged);
        await Assert.That(File.ReadAllText(tracked)).IsEqualTo("unstaged version");
        await Assert.That(File.ReadAllText(System.IO.Path.Combine(restored.Path, "untracked.txt"))).IsEqualTo("untracked content");
        await Assert.That(File.ReadAllText(System.IO.Path.Combine(restored.Path, IgnoredFile))).IsEqualTo("ignored content");
    }

    /// <summary>Verifies protected checkouts cannot be archived.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task PinnedLockedActiveAndExternalCheckoutsAreProtected()
    {
        using var repository = new TemporaryRepository();
        ManagedWorktree item = repository.Service.Create(repository.Path, "CP_protection");
        repository.Service.TogglePin(repository.Path, item.Id);
        await Assert.That(() => repository.Service.ToggleArchive(repository.Path, item.Id)).Throws<InvalidOperationException>();
        repository.Service.TogglePin(repository.Path, item.Id);
        _ = ManagedWorktreeService.Git(repository.Path, WorktreeCommand, "lock", "--reason", "shared checkout", item.Path);
        await Assert.That(() => repository.Service.ToggleArchive(repository.Path, item.Id)).Throws<InvalidOperationException>();
        _ = ManagedWorktreeService.Git(repository.Path, WorktreeCommand, "unlock", item.Path);
        await Assert.That(() => repository.Service.ToggleArchive(item.Path, item.Id)).Throws<InvalidOperationException>();
        ManagedWorktree primary = repository.Service.List(repository.Path).Single(x => !x.IsManaged);
        await Assert.That(primary.Id).IsEqualTo(string.Empty);
        await Assert.That(() => repository.Service.ToggleArchive(repository.Path, primary.Id)).Throws<InvalidOperationException>();
        await Assert.That(Directory.Exists(item.Path)).IsTrue();
    }

    /// <summary>Verifies registry continuity and branch validation without touching real repositories.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task OwnershipSurvivesServiceRestartAndInvalidBranchesFail()
    {
        using var repository = new TemporaryRepository();
        await Assert.That(() => repository.Service.Create(repository.Path, "feature/invalid")).Throws<InvalidOperationException>();
        ManagedWorktree created = repository.Service.Create(repository.Path, "CP_restart");
        var restarted = new ManagedWorktreeService(repository.Storage);
        await Assert.That(restarted.List(repository.Path).Single(x => x.Id == created.Id).IsManaged).IsTrue();
        ManagedWorktree archived = restarted.ToggleArchive(repository.Path, created.Id);
        var restoredService = new ManagedWorktreeService(repository.Storage);
        await Assert.That(restoredService.List(repository.Path).Single(x => x.Id == created.Id).IsArchived).IsTrue();
        await Assert.That(restoredService.ToggleArchive(repository.Path, archived.Id).IsArchived).IsFalse();
    }

    /// <summary>Verifies submodules and embedded repositories prevent any move.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task SubmodulesAndEmbeddedRepositoriesFailWithoutMovingData()
    {
        using var repository = new TemporaryRepository();
        ManagedWorktree item = repository.Service.Create(repository.Path, "CP_nested");
        string commit = ManagedWorktreeService.Git(item.Path, "rev-parse", "HEAD");
        _ = ManagedWorktreeService.Git(item.Path, "update-index", "--add", "--cacheinfo", "160000", commit, "module");
        await Assert.That(() => repository.Service.ToggleArchive(repository.Path, item.Id)).Throws<InvalidOperationException>();
        _ = ManagedWorktreeService.Git(item.Path, "update-index", "--force-remove", "module");
        string nested = System.IO.Path.Combine(item.Path, "nested");
        _ = Directory.CreateDirectory(nested);
        File.WriteAllText(System.IO.Path.Combine(nested, ".git"), "gitdir: unavailable");
        await Assert.That(() => repository.Service.ToggleArchive(repository.Path, item.Id)).Throws<InvalidOperationException>();
        await Assert.That(Directory.Exists(item.Path)).IsTrue();
        await Assert.That(File.ReadAllText(System.IO.Path.Combine(item.Path, TrackedFile))).IsEqualTo(CommittedContent);
    }

    /// <summary>Verifies refresh reconciles a completed move whose final registry write was interrupted.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task RefreshRecoversPendingMoveLocation()
    {
        using var repository = new TemporaryRepository();
        ManagedWorktree item = repository.Service.Create(repository.Path, "CP_recovery");
        string registry = System.IO.Path.Combine(repository.Storage, "registry.json");
        List<ManagedWorktree> records = JsonConvert.DeserializeObject<List<ManagedWorktree>>(File.ReadAllText(registry))!;
        ManagedWorktree pending = records.Single(x => x.Id == item.Id);
        pending.PendingPath = System.IO.Path.Combine(repository.Storage, "archive", item.Id);
        _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(pending.PendingPath)!);
        File.WriteAllText(registry, JsonConvert.SerializeObject(records));
        _ = ManagedWorktreeService.Git(repository.Path, WorktreeCommand, "move", item.Path, pending.PendingPath);
        ManagedWorktree recovered = repository.Service.List(repository.Path).Single(x => x.Id == item.Id);
        await Assert.That(Path.GetFullPath(recovered.Path)).IsEqualTo(Path.GetFullPath(pending.PendingPath));
        await Assert.That(recovered.PendingPath).IsEqualTo(string.Empty);
        await Assert.That(recovered.IsArchived).IsTrue();
        await Assert.That(repository.Service.ToggleArchive(repository.Path, item.Id).Path).IsEqualTo(item.Path);
    }

    /// <summary>Owns only a unique temporary repository and storage directory.</summary>
    private sealed class TemporaryRepository : IDisposable
    {
        /// <summary>Names the dedicated temporary fixture parent.</summary>
        private const string FixtureDirectory = "VSCodex-worktree-tests";

        /// <summary>Stores the verified temporary ownership boundary.</summary>
        private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), FixtureDirectory, Guid.NewGuid().ToString("N"));

        /// <summary>Initializes a new instance of the <see cref="TemporaryRepository"/> class.</summary>
        public TemporaryRepository()
        {
            Path = System.IO.Path.Combine(_root, "repository");
            Storage = System.IO.Path.Combine(_root, "managed");
            _ = Directory.CreateDirectory(Path);
            _ = ManagedWorktreeService.Git(Path, "init");
            _ = ManagedWorktreeService.Git(Path, "config", "user.name", "Worktree Test");
            _ = ManagedWorktreeService.Git(Path, "config", "user.email", "worktree@example.invalid");
            File.WriteAllText(System.IO.Path.Combine(Path, TrackedFile), CommittedContent);
            File.WriteAllText(System.IO.Path.Combine(Path, ".gitignore"), IgnoredFile);
            _ = ManagedWorktreeService.Git(Path, "add", ".");
            _ = ManagedWorktreeService.Git(Path, "commit", "-m", "Initial fixture");
            Service = new(Storage);
        }

        /// <summary>Gets the isolated repository.</summary>
        public string Path { get; }

        /// <summary>Gets the isolated managed storage.</summary>
        public string Storage { get; }

        /// <summary>Gets the service under test.</summary>
        public ManagedWorktreeService Service { get; }

        /// <summary>Removes only the uniquely owned test fixture.</summary>
        public void Dispose()
        {
            ValidateFixtureBoundary(_root);
            foreach (string file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(_root, true);
        }

        /// <summary>Checks the precise ownership boundary before recursive fixture cleanup.</summary>
        /// <param name="root">The fixture directory.</param>
        private static void ValidateFixtureBoundary(string root)
        {
            string expectedParent = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), FixtureDirectory));
            if (string.Equals(System.IO.Path.GetDirectoryName(root), expectedParent, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw new IOException("Temporary fixture ownership validation failed.");
        }
    }
}
