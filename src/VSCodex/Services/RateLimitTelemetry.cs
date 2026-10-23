// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace VSCodex.Services;

/// <summary>Normalizes account quota windows independently of model context estimates.</summary>
internal static class RateLimitTelemetry
{
    /// <summary>Minutes in a weekly quota window.</summary>
    private const int WeeklyMinutes = 10_080;

    /// <summary>Minutes in a five-hour quota window.</summary>
    private const int FiveHourMinutes = 300;

    /// <summary>Maximum quota percentage.</summary>
    private const int MaximumPercent = 100;

    /// <summary>Threshold separating epoch seconds from milliseconds.</summary>
    private const double MillisecondThreshold = 100_000_000_000D;

    /// <summary>Milliseconds per second.</summary>
    private const double MillisecondsPerSecond = 1000D;

    /// <summary>Earliest representable epoch timestamp.</summary>
    private const double MinimumEpochSeconds = -62_135_596_800D;

    /// <summary>Latest representable epoch timestamp.</summary>
    private const double MaximumEpochSeconds = 253_402_300_799D;

    /// <summary>Weekly display label.</summary>
    private const string WeeklyLabel = "Weekly";

    /// <summary>Five-hour display label.</summary>
    private const string FiveHourLabel = "5h";

    /// <summary>Reads current and legacy RPC envelopes in account-pool priority order.</summary>
    /// <param name="root">The response or notification payload.</param>
    /// <returns>Only verified quota windows, deduplicated by duration.</returns>
    internal static IReadOnlyList<Window> Parse(JToken root)
        => Pools(root).SelectMany(ParsePool).GroupBy(window => window.Label).Select(group => group.First()).ToList();

    /// <summary>Reads each supported window from one quota pool.</summary>
    /// <param name="pool">The account quota pool.</param>
    /// <returns>Usable quota windows.</returns>
    private static IEnumerable<Window> ParsePool(JToken pool)
    {
        foreach (string name in new[] { "primary", "secondary", "fiveHour", "five_hour", FiveHourLabel, "weekly", "week", "hourly", "hour" })
        {
            JToken? token = pool[name] ?? (pool["requests"] as JObject)?[name];
            if (token is not JObject)
            {
                continue;
            }

            string label = Label(token, name);
            double? remaining = Remaining(token);
            if (label.Length > 0 && remaining.HasValue)
            {
                int percent = (int)Math.Round(Math.Max(0, Math.Min(MaximumPercent, remaining.Value)), MidpointRounding.AwayFromZero);
                yield return new(label, percent, Reset(token));
            }
        }
    }

    /// <summary>Identifies a quota by duration rather than primary/secondary position.</summary>
    /// <param name="token">The window payload.</param>
    /// <param name="name">The legacy window key.</param>
    /// <returns>The supported window label or empty text.</returns>
    private static string Label(JToken token, string name)
    {
        double? duration = Number(token, "windowDurationMins", "window_duration_mins", "limitWindowMinutes", "limit_window_minutes");
        if (!duration.HasValue)
        {
            return name is "secondary" or "weekly" or "week" ? WeeklyLabel : FiveHourLabel;
        }

        if (duration.Value.CompareTo(WeeklyMinutes) == 0)
        {
            return WeeklyLabel;
        }

        return duration.Value.CompareTo(FiveHourMinutes) == 0 ? FiveHourLabel : string.Empty;
    }

    /// <summary>Reads a remaining percentage or derives it from usage/counts.</summary>
    /// <param name="token">The window payload.</param>
    /// <returns>The supplied percentage, without fabricating missing data.</returns>
    private static double? Remaining(JToken token)
    {
        double? remaining = Number(token, "remainingPercent", "remaining_percent", "remainingPct", "remaining_pct");
        double? used = Number(token, "usedPercent", "used_percent", "usagePercent", "usage_percent");
        if (remaining.HasValue || used.HasValue)
        {
            return remaining ?? (MaximumPercent - used);
        }

        double? available = Number(token, "remaining", "available");
        double? total = Number(token, "limit", "total", "quota");
        return available.HasValue && total > 0 ? available.Value / total.Value * MaximumPercent : null;
    }

    /// <summary>Enumerates account-wide pools before legacy snapshots.</summary>
    /// <param name="root">The RPC envelope.</param>
    /// <returns>Account pool candidates in priority order.</returns>
    private static IEnumerable<JToken> Pools(JToken root)
    {
        foreach (JObject map in root.SelectTokens("$..rateLimitsByLimitId").Concat(root.SelectTokens("$..rate_limits_by_limit_id")).OfType<JObject>())
        {
            if (map["codex"] is JObject codex)
            {
                yield return codex;
            }
        }

        foreach (JObject snapshot in root.SelectTokens("$..rateLimits").Concat(root.SelectTokens("$..rate_limits")).OfType<JObject>())
        {
            JToken? nested = snapshot["rateLimits"] ?? snapshot["rate_limits"];
            yield return nested is JObject ? nested : snapshot;
        }

        if (root is not JObject)
        {
            yield break;
        }

        yield return root;
    }

    /// <summary>Reads the first finite numeric property.</summary>
    /// <param name="token">The source object.</param>
    /// <param name="names">Accepted property names.</param>
    /// <returns>A finite value or null.</returns>
    private static double? Number(JToken token, params string[] names)
    {
        foreach (string name in names)
        {
            if (double.TryParse(token[name]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                && !double.IsNaN(value) && !double.IsInfinity(value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>Reads an absolute reset timestamp.</summary>
    /// <param name="token">The quota window.</param>
    /// <returns>A valid reset timestamp or null.</returns>
    private static DateTimeOffset? Reset(JToken token)
    {
        double? absolute = Number(token, "resetsAt", "resetAt", "resets_at", "reset_at");
        if (absolute.HasValue)
        {
            double seconds = absolute.Value > MillisecondThreshold ? absolute.Value / MillisecondsPerSecond : absolute.Value;
            if (seconds >= MinimumEpochSeconds && seconds <= MaximumEpochSeconds)
            {
                return DateTimeOffset.FromUnixTimeSeconds((long)seconds);
            }
        }

        return ResetFromText(token);
    }

    /// <summary>Reads a textual reset timestamp.</summary>
    /// <param name="token">The quota window.</param>
    /// <returns>A valid timestamp or null.</returns>
    private static DateTimeOffset? ResetFromText(JToken token)
    {
        foreach (string name in new[] { "resetsAt", "resetAt", "resets_at", "reset_at", "reset" })
        {
            if (DateTimeOffset.TryParse(token[name]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    /// <summary>A verified account quota window.</summary>
    internal sealed class Window
    {
        /// <summary>Initializes a new instance of the <see cref="Window"/> class.</summary>
        /// <param name="label">The quota window label.</param>
        /// <param name="remainingPercent">The verified remaining percentage.</param>
        /// <param name="resetsAt">The reset timestamp, if reported.</param>
        internal Window(string label, int remainingPercent, DateTimeOffset? resetsAt)
        {
            Label = label;
            RemainingPercent = remainingPercent;
            ResetsAt = resetsAt;
        }

        /// <summary>Gets the display window.</summary>
        internal string Label { get; }

        /// <summary>Gets the remaining quota percentage.</summary>
        internal int RemainingPercent { get; }

        /// <summary>Gets the reported reset timestamp.</summary>
        internal DateTimeOffset? ResetsAt { get; }
    }
}
