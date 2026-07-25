// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using VSCodex.Building;

namespace VSCodex.Tests;

/// <summary>Verifies the SemVer-to-assembly and SemVer-to-VSIX mapping.</summary>
public sealed class VersionStampTests
{
    /// <summary>Named value used by this type.</summary>
    private const string CoreVersion = "1.2.3";

    /// <summary>Verifies the stable build mapping.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Stable_semver_maps_to_the_highest_revision()
    {
        var stamp = VersionStamp.Parse(CoreVersion);

        await Assert.That(stamp.SemVer).IsEqualTo(CoreVersion);
        await Assert.That(stamp.PackageVersion).IsEqualTo(CoreVersion);
        await Assert.That(stamp.AssemblyVersion).IsEqualTo("1.0.0.0");
        await Assert.That(stamp.FileVersion).IsEqualTo("1.2.3.65535");
        await Assert.That(stamp.VsixVersion).IsEqualTo("1.2.3.65535");
    }

    /// <summary>Verifies that MinVer height and build metadata are preserved without creating a VSIX collision.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task MinVer_preview_maps_height_and_retains_metadata()
    {
        var stamp = VersionStamp.Parse("1.2.3-preview.0.42+build.7");

        await Assert.That(stamp.SemVer).IsEqualTo("1.2.3-preview.0.42+build.7");
        await Assert.That(stamp.PackageVersion).IsEqualTo("1.2.3-preview.0.42");
        await Assert.That(stamp.VsixVersion).IsEqualTo("1.2.3.42");
    }

    /// <summary>Verifies ordered numeric buckets for supported release channels.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Release_channels_map_in_semantic_order()
    {
        var preview = VersionStamp.Parse("1.2.3-preview.1");
        var alpha = VersionStamp.Parse("1.2.3-alpha.1");
        var beta = VersionStamp.Parse("1.2.3-beta.2.42");
        var candidate = VersionStamp.Parse("1.2.3-rc.3");
        var stable = VersionStamp.Parse(CoreVersion);

        await Assert.That(preview.VsixVersion).IsEqualTo("1.2.3.1");
        await Assert.That(alpha.VsixVersion).IsEqualTo("1.2.3.10001");
        await Assert.That(beta.VsixVersion).IsEqualTo("1.2.3.22042");
        await Assert.That(candidate.VsixVersion).IsEqualTo("1.2.3.30003");
        var revisions = new[]
        {
            GetRevision(preview),
            GetRevision(alpha),
            GetRevision(beta),
            GetRevision(candidate),
            GetRevision(stable),
        };
        await Assert.That(revisions.SequenceEqual(revisions.OrderBy(revision => revision))).IsTrue();
    }

    /// <summary>Verifies malformed and unsupported versions fail before a solution build starts.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Invalid_or_unmappable_versions_are_rejected()
    {
        var invalidVersions = new[]
        {
            "1.2",
            "01.2.3",
            "1.2.3-dev.1",
            "1.2.3-rc.10.1",
            "1.2.3-beta.1.1000",
            "65536.0.0",
        };

        foreach (var invalidVersion in invalidVersions)
        {
            var rejected = false;
            try
            {
                _ = VersionStamp.Parse(invalidVersion);
            }
            catch (ArgumentException)
            {
                rejected = true;
            }

            await Assert.That(rejected).IsTrue();
        }
    }

    /// <summary>Gets the numeric revision from a version stamp.</summary>
    /// <param name="stamp">The version stamp.</param>
    /// <returns>The numeric revision.</returns>
    private static int GetRevision(VersionStamp stamp) => Version.Parse(stamp.VsixVersion).Revision;
}
