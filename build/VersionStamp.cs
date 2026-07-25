// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.Linq;
using NuGet.Versioning;

namespace VSCodex.Building;

/// <summary>Contains the validated versions applied to one build invocation.</summary>
public sealed record VersionStamp
{
    /// <summary>Named number used by this type.</summary>
    private const int MinimumReleaseLabelCount = 2;

    /// <summary>Named number used by this type.</summary>
    private const int MaximumReleaseLabelCount = 3;

    /// <summary>Gets the first numeric revision reserved for alpha builds.</summary>
    private const int AlphaRevisionBase = 10_000;

    /// <summary>Gets the first numeric revision reserved for beta builds.</summary>
    private const int BetaRevisionBase = 20_000;

    /// <summary>Gets the first numeric revision reserved for release candidates.</summary>
    private const int RcRevisionBase = 30_000;

    /// <summary>Gets the numeric revision reserved for stable releases.</summary>
    private const int StableRevision = ushort.MaxValue;

    /// <summary>Gets the maximum sequence within one pre-release channel.</summary>
    private const int MaximumChannelSequence = 9_999;

    /// <summary>Gets the maximum release ordinal that can be combined with a MinVer height.</summary>
    private const int MaximumReleaseOrdinal = 9;

    /// <summary>Gets the maximum MinVer height that fits in one release ordinal.</summary>
    private const int MaximumHeight = 999;

    /// <summary>Gets the multiplier used to combine a release ordinal and MinVer height.</summary>
    private const int HeightMultiplier = 1_000;

    /// <summary>Initializes a new instance of the <see cref="VersionStamp"/> class.</summary>
    /// <param name="semVer">The normalized SemVer.</param>
    /// <param name="packageVersion">The package version.</param>
    /// <param name="assemblyVersion">The assembly version.</param>
    /// <param name="fileVersion">The file version.</param>
    /// <param name="vsixVersion">The VSIX version.</param>
    private VersionStamp(
        string semVer,
        string packageVersion,
        string assemblyVersion,
        string fileVersion,
        string vsixVersion)
    {
        SemVer = semVer;
        PackageVersion = packageVersion;
        AssemblyVersion = assemblyVersion;
        FileVersion = fileVersion;
        VsixVersion = vsixVersion;
    }

    /// <summary>Gets the normalized SemVer 2.0 version, including build metadata.</summary>
    public string SemVer { get; }

    /// <summary>Gets the SemVer package version without build metadata.</summary>
    public string PackageVersion { get; }

    /// <summary>Gets the compatibility-oriented assembly version.</summary>
    public string AssemblyVersion { get; }

    /// <summary>Gets the numeric assembly file version.</summary>
    public string FileVersion { get; }

    /// <summary>Gets the numeric four-part VSIX identity version.</summary>
    public string VsixVersion { get; }

    /// <summary>Parses and validates a SemVer value for .NET and VSIX version stamping.</summary>
    /// <param name="value">The SemVer value.</param>
    /// <returns>The derived build versions.</returns>
    public static VersionStamp Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var candidate = value.Trim();
        ValidateThreePartCore(candidate);

        if (!NuGetVersion.TryParse(candidate, out var parsed))
        {
            throw new ArgumentException($"'{candidate}' is not a valid SemVer 2.0 version.", nameof(value));
        }

        ValidateNumericComponent(parsed.Major, nameof(parsed.Major));
        ValidateNumericComponent(parsed.Minor, nameof(parsed.Minor));
        ValidateNumericComponent(parsed.Patch, nameof(parsed.Patch));

        var normalized = parsed.ToFullString();
        var packageVersion = normalized.Split('+')[0];
        var revision = parsed.IsPrerelease ? MapPreReleaseRevision(parsed.Release) : StableRevision;
        var numericVersion = string.Create(
            CultureInfo.InvariantCulture,
            $"{parsed.Major}.{parsed.Minor}.{parsed.Patch}.{revision}");

        return new(
            normalized,
            packageVersion,
            string.Create(CultureInfo.InvariantCulture, $"{parsed.Major}.0.0.0"),
            numericVersion,
            numericVersion);
    }

    /// <summary>Maps a supported SemVer pre-release channel into the VSIX revision range.</summary>
    /// <param name="release">The SemVer pre-release value.</param>
    /// <returns>The numeric VSIX revision.</returns>
    private static int MapPreReleaseRevision(string release)
    {
        var labels = release.Split('.');
        if (labels.Length is < MinimumReleaseLabelCount or > MaximumReleaseLabelCount)
        {
            throw new ArgumentException(
                "VSIX pre-release versions must use preview.N, alpha.N, beta.N, rc.N, or the MinVer channel.N.height form.",
                nameof(release));
        }

        var revisionBase = labels[0].ToLowerInvariant() switch
        {
            "preview" => 0,
            "alpha" => AlphaRevisionBase,
            "beta" => BetaRevisionBase,
            "rc" => RcRevisionBase,
            _ => throw new ArgumentException(
                $"The VSIX pre-release channel '{labels[0]}' is unsupported. Use preview, alpha, beta, or rc.",
                nameof(release)),
        };

        var sequence = labels.Length == 2
            ? ParseNumericLabel(labels[1], "pre-release sequence")
            : EncodeMinVerSequence(labels[1], labels[2]);
        if (sequence > MaximumChannelSequence)
        {
            throw new ArgumentOutOfRangeException(
                nameof(release),
                release,
                $"The encoded pre-release sequence must not exceed {MaximumChannelSequence}.");
        }

        return revisionBase + sequence;
    }

    /// <summary>Encodes a MinVer release ordinal and height into one channel sequence.</summary>
    /// <param name="releaseOrdinalLabel">The release ordinal label.</param>
    /// <param name="heightLabel">The MinVer height label.</param>
    /// <returns>The encoded channel sequence.</returns>
    private static int EncodeMinVerSequence(string releaseOrdinalLabel, string heightLabel)
    {
        var releaseOrdinal = ParseNumericLabel(releaseOrdinalLabel, "pre-release ordinal");
        var height = ParseNumericLabel(heightLabel, "MinVer height");
        if (releaseOrdinal > MaximumReleaseOrdinal)
        {
            throw new ArgumentOutOfRangeException(
                nameof(releaseOrdinalLabel),
                releaseOrdinalLabel,
                $"The pre-release ordinal must not exceed {MaximumReleaseOrdinal} when a MinVer height is present.");
        }

        if (height > MaximumHeight)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heightLabel),
                heightLabel,
                $"The MinVer height must not exceed {MaximumHeight}.");
        }

        return checked((releaseOrdinal * HeightMultiplier) + height);
    }

    /// <summary>Parses a numeric SemVer pre-release label.</summary>
    /// <param name="label">The label.</param>
    /// <param name="description">The label description used in validation errors.</param>
    /// <returns>The numeric value.</returns>
    private static int ParseNumericLabel(string label, string description)
    {
        if (!int.TryParse(label, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
            || (label.Length > 1 && label[0] == '0'))
        {
            throw new ArgumentException(
                $"The {description} '{label}' must be a non-negative integer without leading zeroes.",
                nameof(label));
        }

        return result;
    }

    /// <summary>Validates that a candidate starts with a strict three-part SemVer core.</summary>
    /// <param name="candidate">The candidate version.</param>
    private static void ValidateThreePartCore(string candidate)
    {
        var coreEnd = candidate.IndexOfAny(['-', '+']);
        var core = coreEnd < 0 ? candidate : candidate[..coreEnd];
        var segments = core.Split('.');
        var isValid = segments.Length == MaximumReleaseLabelCount
            && segments.All(segment => int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            && segments.All(segment => segment.Length == 1 || segment[0] != '0');
        if (isValid)
        {
            return;
        }

        throw new ArgumentException(
            $"'{candidate}' must contain exactly three numeric SemVer core components without leading zeroes.",
            nameof(candidate));
    }

    /// <summary>Validates that a numeric version component fits a VSIX version.</summary>
    /// <param name="component">The component.</param>
    /// <param name="name">The component name.</param>
    private static void ValidateNumericComponent(int component, string name)
    {
        if (component <= ushort.MaxValue)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(
            name,
            component,
            $"VSIX numeric version components must not exceed {ushort.MaxValue}.");
    }
}
