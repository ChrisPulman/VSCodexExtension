// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Linq;
using CP.BuildTools;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace VSCodex.Building;

/// <summary>Defines the VSCodex build, test, and VSIX packaging pipeline.</summary>
internal sealed partial class Build : NukeBuild
{
    private const string MinVerArguments =
        "tool run minver -- . --tag-prefix v --minimum-major-minor 0.5 --default-pre-release-identifiers preview.0 --verbosity error";

    private readonly Solution _solution = SolutionFile.ReadSolution();
    private VersionStamp? _versionStamp;

    /// <summary>Gets the build entry point.</summary>
    /// <returns>The process exit code.</returns>
    public static int Main() => Execute<Build>(build => build.Compile);

    private static AbsolutePath SolutionFile => RootDirectory / "src" / "VSCodex.slnx";

    private static AbsolutePath TestProject => RootDirectory / "tests" / "VSCodex.Tests" / "VSCodex.Tests.csproj";

    private static AbsolutePath ArtifactsDirectory => RootDirectory / "output";

    private static AbsolutePath TestResultsDirectory => ArtifactsDirectory / "test-results";

    private static AbsolutePath UnsignedVsixDirectory => ArtifactsDirectory / "unsigned";

    private static AbsolutePath StructureValidator => RootDirectory / "scripts" / "validate_structure.py";

    private AbsolutePath BuiltVsix =>
        RootDirectory / "src" / "VSCodex" / "bin" / Configuration.ToString() / "net48" / "VSCodex.vsix";

    private VersionStamp BuildVersion =>
        _versionStamp ?? throw new InvalidOperationException("ResolveVersion must run before a versioned build target.");

    /// <summary>Gets or sets the configuration to build.</summary>
    [Parameter("Configuration to build - Default is 'Debug' locally or 'Release' on a build server")]
    public readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    /// <summary>Gets or sets an explicit SemVer override. MinVer calculates the value when omitted.</summary>
    [Parameter("Explicit SemVer 2.0 version. When omitted, NUKE calculates it once with MinVer.")]
    public readonly string SemVer = string.Empty;

    /// <summary>Calculates and exports the single authoritative build version.</summary>
    public Target ResolveVersion => target => target
        .Executes(() =>
        {
            DotNetToolRestore(settings => settings.SetProcessWorkingDirectory(RootDirectory));
            var resolvedSemVer = string.IsNullOrWhiteSpace(SemVer) ? CalculateMinVer() : SemVer;
            _versionStamp = VersionStamp.Parse(resolvedSemVer);

            Environment.SetEnvironmentVariable("MINVERVERSIONOVERRIDE", BuildVersion.SemVer);
            Environment.SetEnvironmentVariable("MinVerVersionOverride", BuildVersion.SemVer);

            Log.Information("SemVer = {SemVer}", BuildVersion.SemVer);
            Log.Information("AssemblyVersion = {AssemblyVersion}", BuildVersion.AssemblyVersion);
            Log.Information("FileVersion = {FileVersion}", BuildVersion.FileVersion);
            Log.Information("VSIX version = {VsixVersion}", BuildVersion.VsixVersion);

            if (IsServerBuild)
            {
                this.GitHubSetOutput("semver", BuildVersion.SemVer);
                this.GitHubSetOutput("vsix_version", BuildVersion.VsixVersion);
            }
        });

    /// <summary>Prints the resolved build inputs.</summary>
    public Target Print => target => target
        .DependsOn(ResolveVersion)
        .Executes(() =>
        {
            Log.Information("Configuration = {Configuration}", Configuration);
            Log.Information("MinVerVersionOverride = {Value}", BuildVersion.SemVer);
        });

    /// <summary>Cleans generated build artifacts.</summary>
    public Target Clean => target => target
        .DependsOn(ResolveVersion)
        .Executes(() => ArtifactsDirectory.CreateOrCleanDirectory());

    /// <summary>Restores the solution using the resolved version.</summary>
    public Target Restore => target => target
        .DependsOn(Clean)
        .Executes(() => DotNetRestore(settings => settings
            .SetProjectFile(_solution)
            .SetProperty("MinVerVersionOverride", BuildVersion.SemVer)));

    /// <summary>Builds the complete solution and creates the VSIX container.</summary>
    public Target Compile => target => target
        .DependsOn(Restore, Print)
        .Executes(() => DotNetBuild(settings => ApplyVersion(settings
            .SetProjectFile(_solution)
            .SetConfiguration(Configuration)
            .SetNoRestore(true))));

    /// <summary>Runs the TUnit suite through Microsoft Testing Platform with Cobertura coverage.</summary>
    public Target Test => target => target
        .DependsOn(Compile)
        .Executes(() =>
        {
            TestResultsDirectory.CreateDirectory();
            DotNet(
                $"run --project {TestProject} --configuration {Configuration} --no-build --no-restore -- --no-ansi --coverage --coverage-output-format cobertura --results-directory {TestResultsDirectory}",
                RootDirectory);
        });

    /// <summary>Runs the repository structure validator.</summary>
    public Target ValidateStructure => target => target
        .DependsOn(Test)
        .Executes(() => ProcessTasks
            .StartProcess("python", $"\"{StructureValidator}\"", RootDirectory)
            .AssertZeroExitCode());

    /// <summary>Copies the verified unsigned VSIX to the artifact staging directory.</summary>
    public Target PackageVsix => target => target
        .DependsOn(Test)
        .Executes(() =>
        {
            if (!BuiltVsix.FileExists())
            {
                throw new InvalidOperationException($"The expected VSIX was not produced: {BuiltVsix}");
            }

            UnsignedVsixDirectory.CreateOrCleanDirectory();
            var stagedVsix = BuiltVsix.CopyToDirectory(UnsignedVsixDirectory, ExistsPolicy.FileOverwrite);
            Log.Information("Unsigned VSIX staged at {VsixPath}", stagedVsix);
            if (IsServerBuild)
            {
                this.GitHubSetOutput("vsix_path", stagedVsix);
            }
        });

    /// <summary>Runs the complete pull-request validation graph.</summary>
    public Target Validate => target => target
        .DependsOn(PackageVsix, ValidateStructure);

    private static string CalculateMinVer()
    {
        var output = DotNet(MinVerArguments, RootDirectory, logOutput: false);
        return output
            .Select(output => output.Text?.Trim())
            .LastOrDefault(output => !string.IsNullOrWhiteSpace(output))
            ?? throw new InvalidOperationException("MinVer did not return a version.");
    }

    private DotNetBuildSettings ApplyVersion(DotNetBuildSettings settings) => settings
        .SetProperty("Version", BuildVersion.PackageVersion)
        .SetProperty("PackageVersion", BuildVersion.PackageVersion)
        .SetProperty("AssemblyVersion", BuildVersion.AssemblyVersion)
        .SetProperty("FileVersion", BuildVersion.FileVersion)
        .SetProperty("InformationalVersion", BuildVersion.SemVer)
        .SetProperty("VSCodexVersion", BuildVersion.VsixVersion)
        .SetProperty("MinVerVersionOverride", BuildVersion.SemVer)
        .SetProperty("IncludeSourceRevisionInInformationalVersion", false)
        .SetProperty("ContinuousIntegrationBuild", IsServerBuild)
        .SetProperty("Deterministic", true)
        .SetProperty("TreatWarningsAsErrors", true)
        .SetProperty("CreateVsixContainer", true)
        .SetProperty("DeployExtension", false)
        .SetProperty("VSCodexLaunchVsixInstaller", false)
        .SetProperty("VSCodexUseVsixInstallerDeployment", false);
}
