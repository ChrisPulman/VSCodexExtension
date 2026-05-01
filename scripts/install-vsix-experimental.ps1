param(
    [Parameter(Mandatory = $true)]
    [string]$VsixPath,

    [string]$VsixInstallerPath,

    [string]$RootSuffix = "Exp",

    [string]$InstanceId,

    [string]$LogFile,

    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

function Resolve-VsixInstaller {
    param([string]$ConfiguredPath)

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredPath) -and (Test-Path -LiteralPath $ConfiguredPath)) {
        return (Resolve-Path -LiteralPath $ConfiguredPath).Path
    }

    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($env:VSINSTALLDIR)) {
        $candidates += (Join-Path $env:VSINSTALLDIR "Common7\IDE\VSIXInstaller.exe")
    }

    $programFilesRoots = @(${env:ProgramW6432}, ${env:ProgramFiles}) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique

    foreach ($programFiles in $programFilesRoots) {
        $candidates += Join-Path $programFiles "Microsoft Visual Studio\18\Enterprise\Common7\IDE\VSIXInstaller.exe"
        $candidates += Join-Path $programFiles "Microsoft Visual Studio\18\Professional\Common7\IDE\VSIXInstaller.exe"
        $candidates += Join-Path $programFiles "Microsoft Visual Studio\18\Community\Common7\IDE\VSIXInstaller.exe"
        $candidates += Join-Path $programFiles "Microsoft Visual Studio\17\Enterprise\Common7\IDE\VSIXInstaller.exe"
        $candidates += Join-Path $programFiles "Microsoft Visual Studio\17\Professional\Common7\IDE\VSIXInstaller.exe"
        $candidates += Join-Path $programFiles "Microsoft Visual Studio\17\Community\Common7\IDE\VSIXInstaller.exe"
    }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "Could not locate VSIXInstaller.exe. Set VSCodexVsixInstallerPath to the Visual Studio VSIXInstaller path."
}

function Write-LogTail {
    param([string]$Path)

    if (Test-Path -LiteralPath $Path) {
        Write-Host "VSIXInstaller log tail:"
        Get-Content -LiteralPath $Path -Tail 80 | ForEach-Object { Write-Host $_ }
    }
}

function Get-VsixIdentifier {
    param([string]$Path)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = $archive.Entries | Where-Object { $_.FullName -eq "extension.vsixmanifest" } | Select-Object -First 1
        if ($null -eq $entry) {
            return $null
        }

        $stream = $entry.Open()
        try {
            $reader = [System.IO.StreamReader]::new($stream)
            try {
                [xml]$manifest = $reader.ReadToEnd()
                $namespaceManager = [System.Xml.XmlNamespaceManager]::new($manifest.NameTable)
                $namespaceManager.AddNamespace("vsix", "http://schemas.microsoft.com/developer/vsx-schema/2011")
                $identity = $manifest.SelectSingleNode("/vsix:PackageManifest/vsix:Metadata/vsix:Identity", $namespaceManager)
                return $identity.Id
            }
            finally {
                $reader.Dispose()
            }
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Test-ExtensionInstalled {
    param(
        [string]$ExtensionId,
        [string]$RootSuffix,
        [string]$InstanceId,
        [datetime]$StartedAt
    )

    if ([string]::IsNullOrWhiteSpace($ExtensionId)) {
        return $false
    }

    $visualStudioRoot = Join-Path $env:LOCALAPPDATA "Microsoft\VisualStudio"
    if (-not (Test-Path -LiteralPath $visualStudioRoot)) {
        return $false
    }

    $instancePattern = if ([string]::IsNullOrWhiteSpace($InstanceId)) { "*$RootSuffix" } else { "*_$InstanceId$RootSuffix" }
    $extensionRoots = Get-ChildItem -LiteralPath $visualStudioRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like $instancePattern } |
        ForEach-Object { Join-Path $_.FullName "Extensions" } |
        Where-Object { Test-Path -LiteralPath $_ }

    foreach ($extensionRoot in $extensionRoots) {
        $manifests = Get-ChildItem -LiteralPath $extensionRoot -Recurse -Filter "extension.vsixmanifest" -ErrorAction SilentlyContinue
        foreach ($manifest in $manifests) {
            if ($manifest.LastWriteTime -lt $StartedAt.AddMinutes(-5)) {
                continue
            }

            $content = Get-Content -LiteralPath $manifest.FullName -Raw
            if ($content.Contains($ExtensionId)) {
                Write-Host "Found installed extension at '$($manifest.DirectoryName)'."
                return $true
            }
        }
    }

    return $false
}

$resolvedVsix = Resolve-Path -LiteralPath $VsixPath
$installer = Resolve-VsixInstaller -ConfiguredPath $VsixInstallerPath
$extensionId = Get-VsixIdentifier -Path $resolvedVsix.Path

if ([string]::IsNullOrWhiteSpace($LogFile)) {
    $LogFile = Join-Path ([System.IO.Path]::GetTempPath()) "VSCodexExtension-VSIXInstaller.log"
}

$LogFile = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($LogFile)

$logDirectory = Split-Path -Parent $LogFile
if (-not [string]::IsNullOrWhiteSpace($logDirectory)) {
    New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
}

Remove-Item -LiteralPath $LogFile -Force -ErrorAction SilentlyContinue

$arguments = @(
    "/quiet",
    "/shutdownprocesses",
    "/rootSuffix:$RootSuffix",
    "/logFile:$LogFile"
)

if (-not [string]::IsNullOrWhiteSpace($InstanceId)) {
    $arguments += "/instanceIds:$InstanceId"
}

$arguments += $resolvedVsix.Path

Write-Host "Installing VSIX into Visual Studio root suffix '$RootSuffix'."
$startedAt = Get-Date
& $installer @arguments
$installerExitCode = if ($LASTEXITCODE -is [int]) { $LASTEXITCODE } else { 0 }

$deadline = $startedAt.AddSeconds($TimeoutSeconds)
do {
    $runningInstallers = @(Get-Process VSIXInstaller -ErrorAction SilentlyContinue | Where-Object {
        try {
            $_.StartTime -ge $startedAt.AddSeconds(-2)
        }
        catch {
            $false
        }
    })

    if ($runningInstallers.Count -eq 0) {
        break
    }

    if ((Get-Date) -gt $deadline) {
        Write-LogTail -Path $LogFile
        throw "Timed out waiting for VSIXInstaller child processes to finish."
    }

    Start-Sleep -Seconds 1
}
while ($true)

if ($installerExitCode -ne 0) {
    Write-LogTail -Path $LogFile
    throw "VSIXInstaller failed with exit code $installerExitCode."
}

if (Test-Path -LiteralPath $LogFile) {
    $logText = Get-Content -LiteralPath $LogFile -Raw
    if ($logText -match "NoApplicableSKUs|The operation was not successful|not compatible with the selected version|Install Failed") {
        Write-LogTail -Path $LogFile
        throw "VSIXInstaller reported a failed installation."
    }

    if ($logText -match "completed successfully|PerUserEnabledExtensionsCache|already installed to all applicable products|selected extensions are already installed") {
        Write-Host "VSIXInstaller completed successfully."
        exit 0
    }
}

if (Test-ExtensionInstalled -ExtensionId $extensionId -RootSuffix $RootSuffix -InstanceId $InstanceId -StartedAt $startedAt) {
    Write-Host "VSIXInstaller installed and enabled the extension."
    exit 0
}

Write-LogTail -Path $LogFile
throw "VSIXInstaller did not report a successful installation."
