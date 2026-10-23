// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Newtonsoft.Json;

namespace VSCodex.ViewModels;

/// <summary>Provides UI dispatch and collection lifecycle support.</summary>
public sealed partial class VSCodexToolWindowViewModel
{
    /// <summary>Updates verified account quota windows, preserving previously received telemetry.</summary>
    /// <param name="json">The response or notification JSON.</param>
    private void UpdateRateLimitsFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            IReadOnlyList<RateLimitTelemetry.Window> windows = RateLimitTelemetry.Parse(JToken.Parse(json));
            foreach (RateLimitTelemetry.Window window in windows)
            {
                RateLimitWindowStatus? status = RateLimits.FirstOrDefault(row => row.Label == window.Label);
                if (status is null)
                {
                    status = new RateLimitWindowStatus { Label = window.Label };
                    RateLimits.Add(status);
                }

                status.Remaining = $"{window.RemainingPercent}%";
                status.UsagePercent = window.RemainingPercent;
                status.HasTelemetry = true;
                status.ResetText = window.ResetsAt.HasValue
                    ? window.ResetsAt.Value.ToLocalTime().ToString(window.Label == WeeklyText ? "d MMM HH':'mm" : "HH':'mm")
                    : string.Empty;
                string reset = window.ResetsAt.HasValue
                    ? $"Resets {window.ResetsAt.Value.ToLocalTime():f} (local time)."
                    : "Codex did not supply a reset time.";
                status.ToolTip = $"{window.Label}: {window.RemainingPercent}% remaining. {reset} Updated {_timeProvider.GetLocalNow():HH':'mm}.";
            }

            if (windows.Count > 0)
            {
                foreach (RateLimitWindowStatus absent in RateLimits.Where(row => !row.HasTelemetry).ToList())
                {
                    _ = RateLimits.Remove(absent);
                }

                RateLimitUpdatedAt = $"Codex telemetry {_timeProvider.GetLocalNow():HH':'mm}";
            }
        }
        catch (JsonException)
        {
            // Run output can contain non-telemetry text; preserve the last verified account limits.
        }
    }

    /// <summary>Runs on Ui Thread.</summary>
    /// <param name="action">The action.</param>
    private void RunOnUiThread(Action action)
    {
        if (_uiDispatcher.CheckAccess())
        {
            action();
            return;
        }

        TaskObserver.FireAndForget(_joinableTaskFactory.RunAsync(async () =>
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
            action();
        }).Task);
    }

    /// <summary>Performs the replace operation.</summary>
    /// <typeparam name="T">The t type.</typeparam>
    /// <param name="target">The target.</param>
    /// <param name="items">The items.</param>
    private void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        List<T> snapshot = items.ToList();
        RunOnUiThread(() =>
        {
            target.Clear();
            foreach (T current in snapshot)
            {
                target.Add(current);
            }
        });
    }
}
