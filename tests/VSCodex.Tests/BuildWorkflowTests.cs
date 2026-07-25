// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace VSCodex.Tests;

/// <summary>Verifies the pull-request and manually triggered VSIX delivery workflows.</summary>
public sealed class BuildWorkflowTests
{
    /// <summary>Gets the repository root.</summary>
    private static string RepositoryRoot
    {
        get
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null && !File.Exists(Path.Combine(current.FullName, "Directory.Packages.props")))
            {
                current = current.Parent;
            }

            return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate the VSCodex repository root.");
        }
    }

    /// <summary>Verifies pull requests execute the complete NUKE validation graph without publishing a VSIX.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task BuildOnly_validates_pull_requests_through_NUKE()
    {
        var workflow = ReadText(".github/workflows/BuildOnly.yml");

        await Assert.That(workflow).Contains("pull_request:");
        await Assert.That(workflow).Contains("--target Validate");
        await Assert.That(workflow).Contains("fetch-depth: 0");
        await Assert.That(workflow).Contains("output/test-results/**");
        await Assert.That(workflow).DoesNotContain("VS_MARKETPLACE_PAT");
        await Assert.That(workflow).DoesNotContain("output/unsigned/**");
    }

    /// <summary>Verifies deployment is manual, protected, VSIX-native, and produces only a signed download.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task BuildDeploy_signs_a_VSIX_for_manual_Marketplace_upload()
    {
        var workflow = ReadText(".github/workflows/BuildDeploy.yml");
        var verifier = ReadText("scripts/verify-vsix-signature.ps1");

        await Assert.That(workflow).Contains("workflow_dispatch:");
        await Assert.That(workflow).Contains("if ('${{ github.ref }}' -ne 'refs/heads/main')");
        await Assert.That(workflow).Contains("BuildDeploy must be manually dispatched from the protected main branch.");
        await Assert.That(workflow).Contains("--target PackageVsix");
        await Assert.That(workflow).Contains("--sem-ver \"${{ inputs.version }}\"");
        await Assert.That(workflow).Contains("dismine/windows-app-signing-setup-action@89ae3b032d4bc7a5b98d1a42a34e61ecb6faad64");
        await Assert.That(workflow).Contains("code certificate-store");
        await Assert.That(workflow).Contains("verify-vsix-signature.ps1");
        await Assert.That(workflow).Contains("Upload signed VSIX");
        await Assert.That(workflow).DoesNotContain("ReactiveList.slnx");
        await Assert.That(workflow).DoesNotContain("dotnet nuget push");
        await Assert.That(workflow).DoesNotContain("*.nupkg");
        await Assert.That(workflow).DoesNotContain("VS_MARKETPLACE_PAT");
        await Assert.That(verifier).Contains("VerifySignatures");
        await Assert.That(verifier).Contains("VerifyCertificate");
        await Assert.That(File.Exists(PathFor(".github/workflows/publish-vsix.yml"))).IsFalse();
    }

    /// <summary>Reads a repository file.</summary>
    /// <param name="relativePath">The repository-relative path.</param>
    /// <returns>The file contents.</returns>
    private static string ReadText(string relativePath) => File.ReadAllText(PathFor(relativePath));

    /// <summary>Resolves a repository-relative path.</summary>
    /// <param name="relativePath">The repository-relative path.</param>
    /// <returns>The absolute path.</returns>
    private static string PathFor(string relativePath) =>
        Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
