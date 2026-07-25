// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Newtonsoft.Json;

namespace VSCodex.ViewModels;

/// <summary>Provides UI dispatch and collection lifecycle support.</summary>
public sealed partial class VSCodexToolWindowViewModel
{
    /// <summary>Updates rate Limits From Json.</summary>
    /// <param name="json">The json.</param>
    private void UpdateRateLimitsFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            JToken? limits = FindRateLimitToken(JToken.Parse(json));
            bool changed = false;
            if (limits is not null)
            {
                changed = UpdateRateLimitFromToken(
                    "5h",
                    SelectFirstToken(
                        limits,
                        "primary",
                        "fiveHour",
                        "five_hour",
                        "5h",
                        "hourly",
                        "hour",
                        "requests.primary",
                        "requests.fiveHour",
                        "requests.five_hour",
                        "requests.hourly",
                        "requests.hour"));
                changed |= UpdateRateLimitFromToken(WeeklyText, SelectFirstToken(limits, "secondary", "weekly", "week", "requests.secondary", "requests.weekly", "requests.week"));
            }

            if (changed)
            {
                RateLimitUpdatedAt = $"Codex telemetry {_timeProvider.GetLocalNow():HH':'mm}";
            }
        }
        catch (JsonException)
        {
        }
    }

    /// <summary>Updates rate Limit From Token.</summary>
    /// <param name="label">The label.</param>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> when update Rate Limit From Token succeeds; otherwise, <see langword="false"/>.</returns>
    private bool UpdateRateLimitFromToken(string label, JToken? token)
    {
        if (token is null)
        {
            return false;
        }

        RateLimitWindowStatus? status = RateLimits.FirstOrDefault((x) => string.Equals(x.Label, label, StringComparison.OrdinalIgnoreCase));
        if (status is null)
        {
            return false;
        }

        int? remainingPercent = RemainingPercent(token);
        string remaining = TokenString(token, "remaining", "remainingText", "remaining_text", "available") ?? string.Empty;
        string limit = TokenString(token, "limit", "total", "quota") ?? string.Empty;
        bool updated = UpdateRateLimitUsage(status, token, remainingPercent, remaining, limit);
        return UpdateRateLimitReset(status, label, token) || updated;
    }

    /// <summary>Gets the remaining Percent from a rate limit token.</summary>
    /// <param name="token">The token.</param>
    /// <returns>The remaining percentage, if available.</returns>
    private int? RemainingPercent(JToken token)
    {
        int? remainingPercent = TokenInt(token, "remaining_percent", "remainingPercent", "remaining_pct", "remainingPct");
        int? usedPercent = TokenInt(token, "used_percent", "usedPercent", "usage_percent", "usagePercent");
        return !remainingPercent.HasValue && usedPercent.HasValue ? Numeric100 - usedPercent.Value : remainingPercent;
    }

    /// <summary>Updates rate limit Usage.</summary>
    /// <param name="status">The status.</param>
    /// <param name="token">The token.</param>
    /// <param name="remainingPercent">The remaining Percent.</param>
    /// <param name="remaining">The remaining text.</param>
    /// <param name="limit">The limit text.</param>
    /// <returns><see langword="true"/> when rate limit usage was updated; otherwise, <see langword="false"/>.</returns>
    private bool UpdateRateLimitUsage(RateLimitWindowStatus status, JToken token, int? remainingPercent, string remaining, string limit)
    {
        if (remainingPercent.HasValue)
        {
            int percent = ClampPercent(remainingPercent.Value);
            status.Remaining = $"{percent}%";
            status.UsagePercent = percent;
            return true;
        }

        bool hasRemainingText = !string.IsNullOrWhiteSpace(remaining);
        if (hasRemainingText)
        {
            status.Remaining = string.IsNullOrWhiteSpace(limit) ? remaining : $"{remaining} / {limit}";
        }

        return hasRemainingText
            ? UpdateRateLimitUsagePercent(status, token)
            : UpdateRateLimitUsageFromValues(status, token);
    }

    /// <summary>Updates rate limit Usage From Values.</summary>
    /// <param name="status">The status.</param>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> when rate limit usage was updated; otherwise, <see langword="false"/>.</returns>
    private bool UpdateRateLimitUsageFromValues(RateLimitWindowStatus status, JToken token)
    {
        if (!TryGetRateLimitUsagePercent(token, out int percent))
        {
            return false;
        }

        status.UsagePercent = percent;
        status.Remaining = $"{percent}%";
        return true;
    }

    /// <summary>Updates rate limit Usage Percent.</summary>
    /// <param name="status">The status.</param>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> when rate limit usage was updated; otherwise, <see langword="false"/>.</returns>
    private bool UpdateRateLimitUsagePercent(RateLimitWindowStatus status, JToken token)
    {
        if (!TryGetRateLimitUsagePercent(token, out int percent))
        {
            return false;
        }

        status.UsagePercent = percent;
        return true;
    }

    /// <summary>Attempts to get rate limit Usage Percent.</summary>
    /// <param name="token">The token.</param>
    /// <param name="percent">The percent.</param>
    /// <returns><see langword="true"/> when a rate limit percentage was found; otherwise, <see langword="false"/>.</returns>
    private bool TryGetRateLimitUsagePercent(JToken token, out int percent)
    {
        int? remainingValue = TokenInt(token, "remaining", "available");
        int? limitValue = TokenInt(token, "limit", "total", "quota");
        if (!remainingValue.HasValue || !limitValue.HasValue || limitValue.Value <= 0)
        {
            percent = default;
            return false;
        }

        percent = ClampPercent((int)Math.Round((double)remainingValue.Value / limitValue.Value * Numeric100));
        return true;
    }

    /// <summary>Updates rate limit Reset.</summary>
    /// <param name="status">The status.</param>
    /// <param name="label">The label.</param>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> when the reset value was updated; otherwise, <see langword="false"/>.</returns>
    private bool UpdateRateLimitReset(RateLimitWindowStatus status, string label, JToken token)
    {
        DateTimeOffset? resetAt = TokenResetAt(token);
        if (resetAt.HasValue)
        {
            status.ResetText = FormatRateLimitReset(label, resetAt.Value);
            return true;
        }

        string reset = TokenString(token, "reset", "resetText", "reset_text", "resets") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(reset))
        {
            return false;
        }

        status.ResetText = reset;
        return true;
    }

    /// <summary>Performs the token Reset At operation.</summary>
    /// <param name="token">The token.</param>
    /// <returns>The token Reset At result.</returns>
    private DateTimeOffset? TokenResetAt(JToken token)
    {
        long? absolute = TokenLong(token, "reset_at", "resetAt", "resets_at", "resetsAt");
        if (absolute.HasValue)
        {
            if (absolute.Value > Numeric100000000000L)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(absolute.Value).ToLocalTime();
            }

            if (absolute.Value > Numeric1000000000)
            {
                return DateTimeOffset.FromUnixTimeSeconds(absolute.Value).ToLocalTime();
            }
        }

        long? relative = TokenLong(token, "reset_after_seconds", "resetAfterSeconds", "resets_after_seconds", "resetsAfterSeconds");
        if (relative.HasValue)
        {
            return _timeProvider.GetLocalNow().AddSeconds(relative.Value);
        }

        string? text = TokenString(token, "reset", "resetAt", "reset_at", "resetsAt", "resets_at");
        return !string.IsNullOrWhiteSpace(text) && DateTimeOffset.TryParse(text, out var parsed) ? parsed.ToLocalTime() : null;
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
