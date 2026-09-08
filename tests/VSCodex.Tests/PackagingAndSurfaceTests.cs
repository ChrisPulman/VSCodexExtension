// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;

namespace VSCodex.Tests;

/// <summary>Verifies the packaged extension contract and user-visible Codex surface.</summary>
public sealed class PackagingAndSurfaceTests
{
    /// <summary>Named number used by this type.</summary>
    private const int Numeric3 = 3;

    /// <summary>Path to the view-model source files.</summary>
    private const string ViewModelsDirectory = "src/VSCodex/ViewModels";

    /// <summary>Search pattern for tool-window view-model source files.</summary>
    private const string ToolWindowViewModelSearchPattern = "VSCodexToolWindowViewModel*.cs";

    /// <summary>Gets the repository root containing Directory.Packages.props.</summary>
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

    /// <summary>Verifies that package versions are central and NUKE resolves the release version once with MinVer.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Packages_are_central_and_versions_are_derived_by_MinVer()
    {
        var centralPackages = ReadText("Directory.Packages.props");
        var toolManifest = ReadText(".config/dotnet-tools.json");
        var build = ReadText("build/Build.cs");
        var project = ReadText("src/VSCodex/VSCodex.csproj");
        var manifest = ReadText("src/VSCodex/source.extension.vsixmanifest");

        await Assert.That(centralPackages).Contains("<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>");
        await Assert.That(centralPackages).Contains("<PackageVersion Include=\"NuGet.Versioning\"");
        await Assert.That(centralPackages).Contains("<CPReactiveMemoryMcpServerVersion>1.");
        await Assert.That(toolManifest).Contains("\"minver-cli\"");
        await Assert.That(toolManifest).Contains("\"version\": \"7.0.0\"");
        await Assert.That(build).Contains("Target ResolveVersion");
        await Assert.That(build).Contains("MINVERVERSIONOVERRIDE");
        await Assert.That(build).Contains("VersionStamp.Parse(resolvedSemVer)");
        await Assert.That(project).DoesNotContain("<PackageReference Include=\"MinVer\"");
        await Assert.That(project).Contains("<PackageDownload Include=\"CP.ReactiveMemory.Mcp.Server\"");
        await Assert.That(project).DoesNotContain("<PackageReference Include=\"Microsoft.VSSDK.BuildTools\" Version=");
        await Assert.That(project).Contains("Value=\"$(VSCodexVersion)\"");
        await Assert.That(manifest).Contains("Version=\"$(VSCodexVersion)\"");
    }

    /// <summary>Verifies the Visual Studio 2022 and 2026 compatibility range.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Manifest_supports_Visual_Studio_2022_and_2026()
    {
        var manifest = XDocument.Parse(ReadText("src/VSCodex/source.extension.vsixmanifest"));
        XNamespace ns = "http://schemas.microsoft.com/developer/vsx-schema/2011";
        var targets = manifest.Descendants(ns + "InstallationTarget").ToList();

        await Assert.That(targets).Count().IsEqualTo(Numeric3);
        await Assert.That(targets.All(target => (string?)target.Attribute(nameof(Version)) == "[17.0,19.0)")).IsTrue();
        await Assert.That(targets.Select(target => (string?)target.Attribute("Id"))).Contains("Microsoft.VisualStudio.Community");
        await Assert.That(targets.Select(target => (string?)target.Attribute("Id"))).Contains("Microsoft.VisualStudio.Enterprise");
    }

    /// <summary>Verifies that the WPF surface follows Visual Studio theme resources and remains responsive.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Tool_window_is_theme_aware_wrapped_and_responsive()
    {
        var view = ReadText("src/VSCodex/Views/VSCodexToolWindowControl.xaml");
        var renderer = ReadText("src/VSCodex/Controls/SelectableMarkdownViewer.cs");

        _ = XDocument.Parse(view);
        await Assert.That(view).Contains("MinWidth=\"240\"");
        await Assert.That(view).Contains("MinWidth=\"0\"");
        await Assert.That(view).Contains("MaxWidth=\"{Binding ActualWidth, ElementName=Root}\"");
        await Assert.That(view).Contains("HorizontalContentAlignment=\"Stretch\"");
        await Assert.That(view).Contains("<controls:SelectableMarkdownViewer");
        await Assert.That(view).Contains("HorizontalAlignment=\"Stretch\"");
        await Assert.That(view).Contains("TreeItemAvailableWidthConverter");
        await Assert.That(view).Contains("AncestorType={x:Type TreeViewItem}");
        await Assert.That(view).DoesNotContain("Width=\"{Binding ActualWidth, ElementName=ConversationTree}\"");
        await Assert.That(view).DoesNotContain("Padding=\"0,0,88,0\"");
        await Assert.That(view).Contains("ToolWindowBackgroundBrushKey");
        await Assert.That(view).Contains("ToolWindowTextBrushKey");
        await Assert.That(view).DoesNotMatch("(?i)#[0-9a-f]{6}");
        await Assert.That(renderer).Contains("Document.PageWidth = availableWidth");
        await Assert.That(renderer).Contains("HorizontalContentAlignment = HorizontalAlignment.Stretch");
        await Assert.That(renderer).DoesNotContain("document.Foreground = Foreground");
    }

    /// <summary>Verifies separate settings and the Codex follow-up behavior contract.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Settings_are_separate_and_follow_up_behavior_is_configurable()
    {
        var package = ReadText("src/VSCodex/VSCodexPackage.cs");
        var options = ReadText("src/VSCodex/Options/OptionsProvider.cs")
            + ReadText("src/VSCodex/Options/VSCodexOptionsModel.cs");
        var view = ReadText("src/VSCodex/Views/VSCodexToolWindowControl.xaml");
        var viewCode = ReadText("src/VSCodex/Views/VSCodexToolWindowControl.xaml.cs");
        var viewModel = ReadTextFiles(ViewModelsDirectory, ToolWindowViewModelSearchPattern);

        await Assert.That(package).Contains("ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), \"VSCodex\", \"General\"");
        await Assert.That(options).Contains("class GeneralOptions : DialogPage");
        await Assert.That(options).Contains("Follow-up behavior");
        await Assert.That(view).DoesNotContain("Header=\"Settings\"");
        await Assert.That(view).Contains("Command=\"{Binding SteerCommand}\"");
        await Assert.That(view).Contains("Command=\"{Binding QueueCommand}\"");
        await Assert.That(viewCode).Contains("VsShellUtilities.ShowToolsOptionsPage<OptionsProvider.GeneralOptions>()");
        await Assert.That(viewModel).Contains("SubmitAlternateFollowUpAsync");
        await Assert.That(viewModel).Contains("DefaultFollowUpBehavior");
    }

    /// <summary>Verifies exact-turn steering, interruption, pause, and resume integration.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Run_controls_use_exact_app_server_turns_and_durable_pause()
    {
        var bridge = ReadText("src/VSCodex/Resources/codex-bridge.mjs");
        var viewModel = ReadTextFiles(ViewModelsDirectory, ToolWindowViewModelSearchPattern);
        var reactiveMemory = ReadText("src/VSCodex/Services/ReactiveMemoryService.cs");

        await Assert.That(bridge).Contains("method: 'turn/start'");
        await Assert.That(bridge).Contains("method: 'turn/steer'");
        await Assert.That(bridge).Contains("expectedTurnId: active.turnId");
        await Assert.That(bridge).Contains("method: 'turn/interrupt'");
        await Assert.That(bridge).Contains("type: 'assistant-delta'");
        await Assert.That(viewModel).Contains("PauseActiveRunAsync");
        await Assert.That(viewModel).Contains("await _codex.InterruptAsync(threadId)");
        await Assert.That(viewModel).Contains("SavePauseCheckpointAsync");
        await Assert.That(viewModel).Contains("RestorePauseCheckpointAsync");
        await Assert.That(reactiveMemory).Contains("schema\"] = \"vscodex.pause-checkpoint/1\"");
    }

    /// <summary>Verifies that MCP elicitations can receive approve and decline responses.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Approval_controls_respond_to_MCP_elicitations()
    {
        var viewModel = ReadTextFiles(ViewModelsDirectory, ToolWindowViewModelSearchPattern);
        var orchestrator = ReadText("src/VSCodex/Services/CodexOrchestrator.cs");

        await Assert.That(viewModel).Contains("mcpServer/elicitation/request");
        await Assert.That(viewModel).Contains("request.Id, request.Method, approve");
        await Assert.That(orchestrator).Contains("[\"action\"] = approve ? \"accept\" : \"decline\"");
        await Assert.That(orchestrator).Contains("[\"content\"] = JValue.CreateNull()");
    }

    /// <summary>Verifies that ReactiveMemory is bundled and cannot be disabled or removed.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task ReactiveMemory_is_required_bundled_and_non_removable()
    {
        var project = ReadText("src/VSCodex/VSCodex.csproj");
        var models = ReadText("src/VSCodex/Models/McpServerDefinition.cs");
        var config = ReadText("src/VSCodex/Services/McpConfigService.cs");

        await Assert.That(project).Contains("IncludeReactiveMemoryServerInVsix");
        await Assert.That(project).Contains("<VSIXSubPath>ReactiveMemory\\%(RecursiveDir)</VSIXSubPath>");
        await Assert.That(models).Contains("public bool IsRequired");
        await Assert.That(models).Contains("public bool CanDisable");
        await Assert.That(models).Contains("public bool CanRemove");
        await Assert.That(config).Contains("CreateRequiredReactiveMemoryServer");
        await Assert.That(config).Contains("IsRequired = true");
    }

    /// <summary>Verifies that source and project configuration do not suppress diagnostics.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Source_contains_no_warning_or_analyzer_suppressions()
    {
        var files = Directory
            .EnumerateFiles(RepositoryRoot, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".props", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var contents = files.Select(path => (Path: path, Text: File.ReadAllText(path))).ToList();

        var pragmaSuppression = "#pragma " + "warning";
        var projectSuppression = "<No" + "Warn>";
        var attributeSuppression = "Suppress" + "Message(";
        await Assert.That(contents.Where(file => file.Text.Contains(pragmaSuppression, StringComparison.Ordinal))).IsEmpty();
        await Assert.That(contents.Where(file => file.Text.Contains(projectSuppression, StringComparison.Ordinal))).IsEmpty();
        await Assert.That(contents.Where(file => file.Text.Contains(attributeSuppression, StringComparison.Ordinal))).IsEmpty();
    }

    /// <summary>Verifies that the generated VSIX contains its required durable-memory runtime.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Release_vsix_contains_ReactiveMemory_and_versioned_manifest()
    {
        var vsixPath = PathFor("src/VSCodex/bin/Release/net48/VSCodex.vsix");
        await Assert.That(File.Exists(vsixPath)).IsTrue();

        using var archive = ZipFile.OpenRead(vsixPath);
        var entries = archive.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToList();
        var manifestEntry = archive.GetEntry("extension.vsixmanifest");
        await Assert.That(entries.Any(entry => entry.StartsWith("ReactiveMemory/", StringComparison.Ordinal))).IsTrue();
        await Assert.That(entries).Contains("VSCodex.dll");
        await Assert.That(manifestEntry).IsNotNull();

        using var reader = new StreamReader(manifestEntry!.Open());
        var manifest = await reader.ReadToEndAsync();
        var manifestDocument = XDocument.Parse(manifest);
        XNamespace ns = "http://schemas.microsoft.com/developer/vsx-schema/2011";
        var identityVersion = (string?)manifestDocument.Descendants(ns + "Identity").Single().Attribute(nameof(Version));
        var isNumericVersion = Version.TryParse(identityVersion, out var parsedVersion)
            && parsedVersion.Revision >= 0;

        await Assert.That(isNumericVersion).IsTrue();
        await Assert.That(manifest).DoesNotContain("$(VSCodexVersion)");
    }

    /// <summary>Verifies that the JavaScript bridge parses and its resilient parser and model catalog pass.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Bridge_syntax_and_resilient_parser_are_valid()
    {
        var script = PathFor("src/VSCodex/Resources/codex-bridge.mjs");
        var syntax = await RunProcessAsync("node", $"--check \"{script}\"");
        var parser = await RunProcessAsync("node", $"\"{script}\" --self-test-resilient-parser");
        var models = await RunProcessAsync("node", $"\"{script}\" --self-test-model-catalog");

        await Assert.That(syntax.ExitCode).IsEqualTo(0);
        await Assert.That(parser.ExitCode).IsEqualTo(0);
        await Assert.That(models.ExitCode).IsEqualTo(0);
        await Assert.That(parser.Output).Contains("\"finalResponse\":\"Hi from parser\"");
        await Assert.That(parser.Output).Contains("\"ignoredCount\":1");
        await Assert.That(models.Output).Contains("\"transportEffort\":\"max\"");
        await Assert.That(models.Output).Contains("\"model\":\"gpt-5.3-codex-spark\"");
    }

    /// <summary>Runs a child process without blocking its redirected streams.</summary>
    /// <param name="fileName">The executable name.</param>
    /// <param name="arguments">The command arguments.</param>
    /// <returns>The exit code and captured output.</returns>
    private static async Task<ProcessResult> RunProcessAsync(string fileName, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = RepositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        _ = process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new(process.ExitCode, await outputTask, await errorTask);
    }

    /// <summary>Reads a repository file.</summary>
    /// <param name="relativePath">The repository-relative path.</param>
    /// <returns>The file contents.</returns>
    private static string ReadText(string relativePath) => File.ReadAllText(PathFor(relativePath));

    /// <summary>Reads and combines repository files matching a search pattern.</summary>
    /// <param name="relativeDirectory">The repository-relative directory.</param>
    /// <param name="searchPattern">The file search pattern.</param>
    /// <returns>The combined file contents in stable path order.</returns>
    private static string ReadTextFiles(string relativeDirectory, string searchPattern) =>
        string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(PathFor(relativeDirectory), searchPattern, SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(File.ReadAllText));

    /// <summary>Resolves a repository-relative path.</summary>
    /// <param name="relativePath">The repository-relative path.</param>
    /// <returns>The absolute path.</returns>
    private static string PathFor(string relativePath) => Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Contains captured process output.</summary>
    /// <param name="ExitCode">The process exit code.</param>
    /// <param name="Output">The standard output.</param>
    /// <param name="Error">The standard error.</param>
    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
