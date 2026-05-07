param(
    [Parameter(Mandatory = $true)]
    [string]$VsixPath,

    [string]$VsixInstallerPath,

    [switch]$Wait,

    [switch]$ResolveOnly
)

$ErrorActionPreference = 'Stop'

function Add-Candidate {
    param(
        [System.Collections.Generic.List[string]]$Candidates,
        [string]$Path
    )

    if (-not [string]::IsNullOrWhiteSpace($Path)) {
        [void]$Candidates.Add($Path)
    }
}

function Add-VisualStudioInstallPathCandidates {
    param(
        [System.Collections.Generic.List[string]]$Candidates,
        [string]$InstallPath
    )

    if ([string]::IsNullOrWhiteSpace($InstallPath)) {
        return
    }

    Add-Candidate $Candidates (Join-Path $InstallPath 'Common7\IDE\VSIXInstaller.exe')
}

function Resolve-VsixInstallerPath {
    param([string]$ConfiguredPath)

    $candidates = [System.Collections.Generic.List[string]]::new()
    Add-Candidate $candidates $ConfiguredPath

    if (-not [string]::IsNullOrWhiteSpace($env:DevEnvDir)) {
        Add-Candidate $candidates (Join-Path $env:DevEnvDir 'VSIXInstaller.exe')
    }

    if (-not [string]::IsNullOrWhiteSpace($env:VSINSTALLDIR)) {
        Add-VisualStudioInstallPathCandidates $candidates $env:VSINSTALLDIR
    }

    $programFiles = [Environment]::GetEnvironmentVariable('ProgramFiles')
    $programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')

    if (-not [string]::IsNullOrWhiteSpace($programFiles)) {
        foreach ($major in @('18', '2022')) {
            foreach ($sku in @('Enterprise', 'Professional', 'Community', 'BuildTools', 'Preview', 'Insiders')) {
                Add-Candidate $candidates (Join-Path $programFiles "Microsoft Visual Studio\$major\$sku\Common7\IDE\VSIXInstaller.exe")
            }
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $vswhere = Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'
        if (Test-Path -LiteralPath $vswhere -PathType Leaf) {
            $installPaths = & $vswhere -all -products * -requires Microsoft.VisualStudio.Component.CoreEditor -property installationPath 2>$null
            foreach ($installPath in $installPaths) {
                if ($installPath -like '*\Microsoft Visual Studio\*') {
                    Add-VisualStudioInstallPathCandidates $candidates $installPath
                }
            }
        }

        Add-Candidate $candidates (Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\resources\app\ServiceHub\Services\Microsoft.VisualStudio.Setup.Service\VSIXInstaller.exe')
        Add-Candidate $candidates (Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\resources\app\ServiceHub\Services\Microsoft.VisualStudio.Setup.Service\VsixInstaller\VSIXInstaller.exe')
    }

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).ProviderPath
        }
    }

    throw "VSIXInstaller.exe was not found. Checked:`n$($candidates -join "`n")"
}

$resolvedVsix = Resolve-Path -LiteralPath $VsixPath
$installer = Resolve-VsixInstallerPath $VsixInstallerPath

if ($ResolveOnly) {
    [pscustomobject]@{
        VsixPath = $resolvedVsix.ProviderPath
        VSIXInstallerPath = $installer
    } | ConvertTo-Json
    exit 0
}

Write-Host "Launching Visual Studio VSIXInstaller for $($resolvedVsix.ProviderPath)"
$process = Start-Process `
    -FilePath $installer `
    -ArgumentList @("`"$($resolvedVsix.ProviderPath)`"") `
    -WorkingDirectory (Split-Path -Parent $resolvedVsix.ProviderPath) `
    -PassThru

if ($Wait) {
    $process.WaitForExit()
    exit $process.ExitCode
}
