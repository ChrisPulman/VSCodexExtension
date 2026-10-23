// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using VSCodex.Core.Automations;
using VSCodex.Services;

namespace VSCodex.Tests;

/// <summary>Verifies durable recurrence and shared state using independent file-backed hosts.</summary>
public sealed class LocalAutomationRunnerTests
{
    /// <summary>Defines the recurrence interval.</summary>
    private const int IntervalMinutes = 60;

    /// <summary>Defines timeout plus stale-owner recovery margin.</summary>
    private const int RecoveryMinutes = 16;

    /// <summary>Defines downtime used for recurrence coalescing.</summary>
    private const int MissedDays = 7;

    /// <summary>Defines the successful executor response.</summary>
    private const string DoneResult = "done";

    /// <summary>Defines the persisted error.</summary>
    private const string FailedResult = "failed";

    /// <summary>Verifies default pause and overdue coalescing against persisted state.</summary>
    /// <returns>The test completion.</returns>
    [Test]
    public async Task Disabled_jobs_do_not_run_and_overdue_jobs_coalesce()
    {
        using var scope = new AutomationScope();
        LocalAutomation job = AddJob(scope, false);
        DateTimeOffset now = DateTimeOffset.UnixEpoch.AddDays(MissedDays);
        int runs = 0;
        var runner = new LocalAutomationRunner(
            () => now,
            (_, _) =>
            {
                runs++;
                return Task.FromResult(DoneResult);
            },
            scope.First);
        await runner.TickAsync([job], CancellationToken.None);
        await Assert.That(runs).IsEqualTo(0);
        _ = scope.Second.TryUpdate(jobs => jobs.Single().Enabled = true);
        await runner.TickAsync([job], CancellationToken.None);
        await runner.TickAsync([job], CancellationToken.None);
        LocalAutomation latest = scope.Second.TryUpdate()!.Single();
        await Assert.That(runs).IsEqualTo(1);
        await Assert.That(latest.NextDue).IsEqualTo(now.AddMinutes(IntervalMinutes));
        await Assert.That(latest.LastResult).IsEqualTo(DoneResult);
        await Assert.That(latest.Unread).IsTrue();
    }

    /// <summary>Verifies separate host schedulers cannot launch the same claimed schedule twice.</summary>
    /// <returns>The test completion.</returns>
    [Test]
    public async Task Two_hosts_cannot_overlap_and_claim_is_saved_before_execution()
    {
        using var scope = new AutomationScope();
        LocalAutomation job = AddJob(scope);
        var completion = new TaskCompletionSource<string>();
        int runs = 0;
        var first = new LocalAutomationRunner(
            () => DateTimeOffset.UnixEpoch,
            (_, _) =>
            {
                runs++;
                return completion.Task;
            },
            scope.First);
        var second = new LocalAutomationRunner(() => DateTimeOffset.UnixEpoch, (_, _) => Task.FromResult(FailedResult), scope.Second);
        Task running = first.TickAsync([job], CancellationToken.None);
        LocalAutomation claimed = scope.Second.TryUpdate()!.Single();
        await second.TickAsync([job], CancellationToken.None);
        await Assert.That(runs).IsEqualTo(1);
        await Assert.That(claimed.ClaimId).IsNotEqualTo(string.Empty);
        await Assert.That(claimed.ClaimExpires).IsEqualTo(DateTimeOffset.UnixEpoch.AddMinutes(RecoveryMinutes));
        completion.SetResult(DoneResult);
        await running;
        await Assert.That(scope.Second.TryUpdate()!.Single().LastResult).IsEqualTo(DoneResult);
    }

    /// <summary>Verifies competing background hosts obtain only one durable owner.</summary>
    /// <returns>The test completion.</returns>
    [Test]
    public async Task Simultaneous_host_claims_have_one_owner()
    {
        using var scope = new AutomationScope();
        LocalAutomation job = AddJob(scope);
        Task<LocalAutomationClaim?> first = Task.Run(() => scope.First.TryClaim(job.Id, DateTimeOffset.UnixEpoch, TimeSpan.FromMinutes(RecoveryMinutes)));
        Task<LocalAutomationClaim?> second = Task.Run(() => scope.Second.TryClaim(job.Id, DateTimeOffset.UnixEpoch, TimeSpan.FromMinutes(RecoveryMinutes)));
        LocalAutomationClaim?[] claims = await Task.WhenAll(first, second);
        using LocalAutomationClaim? firstOwner = claims[0];
        using LocalAutomationClaim? secondOwner = claims[1];
        await Assert.That(claims.Count(claim => claim is not null)).IsEqualTo(1);
    }

    /// <summary>Verifies failed execution persists a notification and releases its claim.</summary>
    /// <returns>The test completion.</returns>
    [Test]
    public async Task Failed_job_records_notification_and_releases_claim()
    {
        using var scope = new AutomationScope();
        LocalAutomation job = AddJob(scope);
        var runner = new LocalAutomationRunner(
            () => DateTimeOffset.UnixEpoch,
            (_, _) => Task.FromException<string>(new InvalidOperationException(FailedResult)),
            scope.First);
        await runner.TickAsync([job], CancellationToken.None);
        LocalAutomation latest = scope.Second.TryUpdate()!.Single();
        await Assert.That(latest.LastFailed).IsTrue();
        await Assert.That(latest.Unread).IsTrue();
        await Assert.That(latest.LastResult).IsEqualTo(FailedResult);
        await Assert.That(latest.ClaimId).IsEqualTo(string.Empty);
    }

    /// <summary>Verifies host cancellation reaches the executor and records interruption.</summary>
    /// <returns>The test completion.</returns>
    [Test]
    public async Task Host_cancellation_interrupts_current_job()
    {
        using var scope = new AutomationScope();
        LocalAutomation job = AddJob(scope);
        using var cancellation = new CancellationTokenSource();
        var runner = new LocalAutomationRunner(
            () => DateTimeOffset.UnixEpoch,
            async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return string.Empty;
            },
            scope.First);
        Task running = runner.TickAsync([job], cancellation.Token);
        cancellation.Cancel();
        await running;
        LocalAutomation latest = scope.Second.TryUpdate()!.Single();
        await Assert.That(latest.LastFailed).IsTrue();
        await Assert.That(latest.Unread).IsTrue();
    }

    /// <summary>Verifies refreshed mutations preserve schedules added by a different host.</summary>
    /// <returns>The test completion.</returns>
    [Test]
    public async Task Mutations_reload_latest_rows_instead_of_overwriting_other_hosts()
    {
        using var scope = new AutomationScope();
        LocalAutomation first = AddJob(scope);
        _ = scope.First.TryUpdate();
        var second = new LocalAutomation { Name = DoneResult };
        _ = scope.Second.TryUpdate(jobs => jobs.Add(second));
        _ = scope.First.TryUpdate(jobs => jobs.Single(item => item.Id == first.Id).Enabled = false);
        List<LocalAutomation> latest = scope.Second.TryUpdate()!;
        await Assert.That(latest.Any(item => item.Id == second.Id)).IsTrue();
        await Assert.That(latest.Single(item => item.Id == first.Id).Enabled).IsFalse();
    }

    /// <summary>Verifies an in-flight completion cannot resurrect a deleted schedule.</summary>
    /// <returns>The test completion.</returns>
    [Test]
    public async Task Deleted_job_is_not_resurrected_by_completion()
    {
        using var scope = new AutomationScope();
        LocalAutomation job = AddJob(scope);
        using LocalAutomationClaim claim = scope.First.TryClaim(job.Id, DateTimeOffset.UnixEpoch, TimeSpan.FromMinutes(RecoveryMinutes))!;
        _ = scope.Second.TryUpdate(jobs => jobs.Clear());
        claim.Job.LastResult = DoneResult;
        bool completed = scope.First.TryComplete(claim.Job);
        await Assert.That(completed).IsTrue();
        await Assert.That(scope.Second.TryUpdate()!).IsEmpty();
    }

    /// <summary>Verifies completion merges results without undoing a pause or prompt edit.</summary>
    /// <returns>The test completion.</returns>
    [Test]
    public async Task Completion_preserves_pause_and_prompt_changes()
    {
        using var scope = new AutomationScope();
        LocalAutomation job = AddJob(scope);
        using LocalAutomationClaim claim = scope.First.TryClaim(job.Id, DateTimeOffset.UnixEpoch, TimeSpan.FromMinutes(RecoveryMinutes))!;
        _ = scope.Second.TryUpdate(jobs =>
        {
            jobs.Single().Enabled = false;
            jobs.Single().Prompt = DoneResult;
        });
        claim.Job.LastResult = DoneResult;
        _ = scope.First.TryComplete(claim.Job);
        LocalAutomation latest = scope.Second.TryUpdate()!.Single();
        await Assert.That(latest.Enabled).IsFalse();
        await Assert.That(latest.Prompt).IsEqualTo(DoneResult);
        await Assert.That(latest.LastResult).IsEqualTo(DoneResult);
    }

    /// <summary>Verifies recovery waits for expiry and a live handle prevents takeover.</summary>
    /// <returns>The test completion.</returns>
    [Test]
    public async Task Expired_owner_recovers_after_process_lease_is_released()
    {
        using var scope = new AutomationScope();
        LocalAutomation job = AddJob(scope);
        using LocalAutomationClaim first = scope.First.TryClaim(job.Id, DateTimeOffset.UnixEpoch, TimeSpan.FromMinutes(RecoveryMinutes))!;
        DateTimeOffset expired = DateTimeOffset.UnixEpoch.AddMinutes(RecoveryMinutes);
        using LocalAutomationClaim? whileAlive = scope.Second.TryClaim(job.Id, expired, TimeSpan.FromMinutes(RecoveryMinutes));
        await Assert.That(whileAlive).IsNull();
        first.Dispose();
        using LocalAutomationClaim? beforeExpiry = scope.Second.TryClaim(job.Id, DateTimeOffset.UnixEpoch, TimeSpan.FromMinutes(RecoveryMinutes));
        await Assert.That(beforeExpiry).IsNull();
        using LocalAutomationClaim? recovered = scope.Second.TryClaim(job.Id, expired, TimeSpan.FromMinutes(RecoveryMinutes));
        await Assert.That(recovered).IsNotNull();
        await Assert.That(recovered!.Job.ClaimId).IsNotEqualTo(first.Job.ClaimId);
        first.Job.LastResult = FailedResult;
        _ = scope.First.TryComplete(first.Job);
        LocalAutomation latest = scope.Second.TryUpdate()!.Single();
        await Assert.That(latest.ClaimId).IsEqualTo(recovered.Job.ClaimId);
        await Assert.That(latest.LastResult).IsEqualTo(string.Empty);
    }

    /// <summary>Verifies a busy state transaction does not wait or mutate stale state.</summary>
    /// <returns>The test completion.</returns>
    [Test]
    public async Task Busy_state_lease_returns_without_applying_mutation()
    {
        using var scope = new AutomationScope();
        _ = AddJob(scope);
        using var busy = new FileStream($"{scope.StatePath}.lease", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        bool mutated = false;
        List<LocalAutomation>? result = scope.Second.TryUpdate(_ => mutated = true);
        await Assert.That(result).IsNull();
        await Assert.That(mutated).IsFalse();
    }

    /// <summary>Creates a durable due schedule for a test host pair.</summary>
    /// <param name="scope">The isolated host state.</param>
    /// <param name="enabled">Whether the user opted into execution.</param>
    /// <returns>The saved schedule snapshot.</returns>
    private static LocalAutomation AddJob(AutomationScope scope, bool enabled = true)
    {
        var job = new LocalAutomation { Enabled = enabled, IntervalMinutes = IntervalMinutes, NextDue = DateTimeOffset.UnixEpoch };
        _ = scope.First.TryUpdate(jobs => jobs.Add(job));
        return job;
    }

    /// <summary>Owns isolated temporary state for two independent host coordinators.</summary>
    private sealed class AutomationScope : IDisposable
    {
        /// <summary>Stores the temporary root.</summary>
        private readonly string _root = Path.Combine(Path.GetTempPath(), "VSCodex-automation-tests", Guid.NewGuid().ToString("N"));

        /// <summary>Initializes a new instance of the <see cref="AutomationScope"/> class.</summary>
        public AutomationScope()
        {
            StatePath = Path.Combine(_root, "automations.json");
            First = new(StatePath);
            Second = new(StatePath);
        }

        /// <summary>Gets the temporary shared state path.</summary>
        public string StatePath { get; }

        /// <summary>Gets the first host coordinator.</summary>
        public LocalAutomationCoordinator First { get; }

        /// <summary>Gets the second host coordinator.</summary>
        public LocalAutomationCoordinator Second { get; }

        /// <summary>Removes temporary files after owned claims have been released.</summary>
        public void Dispose() => Directory.Delete(_root, true);
    }
}
