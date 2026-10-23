// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Newtonsoft.Json.Linq;
using VSCodex.Services;

namespace VSCodex.Tests;

/// <summary>Verifies account telemetry normalization across supported Codex schemas.</summary>
public sealed class RateLimitTelemetryTests
{
    /// <summary>Weekly display label.</summary>
    private const string WeeklyLabel = "Weekly";

    /// <summary>Number of quota windows in a dual-window account.</summary>
    private const int DualWindows = 2;

    /// <summary>Rounded remaining quota in the primary sample.</summary>
    private const int PrimaryRemaining = 83;

    /// <summary>Remaining quota in the weekly sample.</summary>
    private const int WeeklyRemaining = 58;

    /// <summary>Sample reset timestamp.</summary>
    private const long SampleReset = 1_793_000_000;

    /// <summary>Maximum percentage.</summary>
    private const int FullQuota = 100;

    /// <summary>Rounded decimal remaining sample.</summary>
    private const int RoundedRemaining = 72;

    /// <summary>Fractional quota sample.</summary>
    private const int FractionRemaining = 75;

    /// <summary>Partial quota sample.</summary>
    private const int PartialRemaining = 77;

    /// <summary>Verifies the current account pool overrides an incomplete legacy snapshot.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task Current_pool_supplies_weekly_window_and_overrides_legacy()
    {
        const string json = """
            {"result":{"rateLimits":{"primary":{"usedPercent":90}},"rateLimitsByLimitId":{
              "codex":{"primary":{"usedPercent":17.2,"windowDurationMins":300},
                       "secondary":{"usedPercent":42,"windowDurationMins":10080,"resetsAt":1793000000}},
              "codex_other":{"secondary":{"usedPercent":99,"windowDurationMins":10080}}}}}
            """;
        var windows = RateLimitTelemetry.Parse(JToken.Parse(json));
        await Assert.That(windows.Count).IsEqualTo(DualWindows);
        await Assert.That(windows.Single(window => window.Label == "5h").RemainingPercent).IsEqualTo(PrimaryRemaining);
        await Assert.That(windows.Single(window => window.Label == WeeklyLabel).RemainingPercent).IsEqualTo(WeeklyRemaining);
        await Assert.That(windows.Single(window => window.Label == WeeklyLabel).ResetsAt).IsEqualTo(DateTimeOffset.FromUnixTimeSeconds(SampleReset));
    }

    /// <summary>Verifies duration takes precedence over primary and secondary ordering.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task Duration_identifies_weekly_even_when_it_is_primary()
    {
        var windows = RateLimitTelemetry.Parse(JToken.Parse("""
            {"rateLimits":{"primary":{"usedPercent":120,"windowDurationMins":10080},
              "secondary":{"usedPercent":-4,"windowDurationMins":300}}}
            """));
        await Assert.That(windows.Single(window => window.Label == WeeklyLabel).RemainingPercent).IsEqualTo(0);
        await Assert.That(windows.Single(window => window.Label == "5h").RemainingPercent).IsEqualTo(FullQuota);
    }

    /// <summary>Verifies the live weekly-only account shape has no invented five-hour window.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task Weekly_only_account_has_one_correctly_named_window()
    {
        const int expectedRemaining = 82;
        var windows = RateLimitTelemetry.Parse(JToken.Parse("""
            {"result":{"rateLimits":{"primary":{"usedPercent":18,"windowDurationMins":10080},"secondary":null},
              "rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":18,"windowDurationMins":10080},"secondary":null}}}}
            """));
        await Assert.That(windows.Count).IsEqualTo(1);
        await Assert.That(windows[0].Label).IsEqualTo(WeeklyLabel);
        await Assert.That(windows[0].RemainingPercent).IsEqualTo(expectedRemaining);
    }

    /// <summary>Verifies legacy notifications and millisecond reset timestamps remain supported.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task Legacy_notification_supports_decimal_and_absolute_reset()
    {
        var windows = RateLimitTelemetry.Parse(JToken.Parse("""
            {"params":{"rate_limits":{"primary":{"remaining_percent":71.5,"reset_at":1793000000000},
              "secondary":{"remaining":15,"limit":20}}}}
            """));
        await Assert.That(windows[0].RemainingPercent).IsEqualTo(RoundedRemaining);
        await Assert.That(windows[0].ResetsAt).IsEqualTo(DateTimeOffset.FromUnixTimeSeconds(SampleReset));
        await Assert.That(windows[1].RemainingPercent).IsEqualTo(FractionRemaining);
    }

    /// <summary>Verifies missing, malformed, unsupported, and unrelated data cannot imply a quota.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task Unavailable_data_never_becomes_a_full_or_zero_account_quota()
    {
        foreach (string json in new[]
        {
            "{}",
            "{\"rateLimits\":{\"primary\":null,\"secondary\":null}}",
            "{\"rateLimits\":{\"primary\":{\"usedPercent\":\"NaN\"}}}",
            "{\"rateLimits\":{\"primary\":{\"usedPercent\":10,\"windowDurationMins\":60}}}",
            "{\"rateLimits\":{\"primary\":{\"resetsAt\":1793000000}}}",
            "{\"rateLimitsByLimitId\":{\"model-other\":{\"primary\":{\"usedPercent\":20}}}}",
            "{\"context\":{\"remainingPercent\":100}}",
        })
        {
            await Assert.That(RateLimitTelemetry.Parse(JToken.Parse(json)).Count).IsEqualTo(0);
        }
    }

    /// <summary>Verifies a partial response leaves absent windows available for cached retention.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task Partial_response_contains_only_verified_window()
    {
        var windows = RateLimitTelemetry.Parse(JToken.Parse("""
            {"result":{"rateLimits":{"primary":{"usedPercent":23,"resetsAt":9e30},"secondary":null}}}
            """));
        await Assert.That(windows.Count).IsEqualTo(1);
        await Assert.That(windows[0].Label).IsEqualTo("5h");
        await Assert.That(windows[0].RemainingPercent).IsEqualTo(PartialRemaining);
        await Assert.That(windows[0].ResetsAt).IsNull();
    }
}
