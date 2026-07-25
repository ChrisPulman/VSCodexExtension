[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $VsixPath,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $UnsignedVsixPath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string] $CertificateFingerprint
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-VsixStructure {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entries = @($archive.Entries)
        $entryNames = @(
            $entries |
                ForEach-Object { $_.FullName.Replace('\', '/') }
        )
        $duplicateNames = @(
            $entryNames |
                Group-Object { $_.ToLowerInvariant() } |
                Where-Object { $_.Count -gt 1 } |
                ForEach-Object { $_.Group[0] }
        )
        if ($duplicateNames.Count -gt 0) {
            throw "The VSIX contains duplicate package entries: $($duplicateNames -join ', ')."
        }

        $requiredEntries = @(
            '[Content_Types].xml',
            'extension.vsixmanifest',
            'catalog.json',
            'manifest.json'
        )
        $missingEntries = @(
            $requiredEntries |
                Where-Object { $entryNames -cnotcontains $_ }
        )
        if ($missingEntries.Count -gt 0) {
            throw "The VSIX is missing required root package entries: $($missingEntries -join ', ')."
        }

        foreach ($entry in $entries) {
            $stream = $entry.Open()
            try {
                $buffer = New-Object byte[] 81920
                while ($stream.Read($buffer, 0, $buffer.Length) -gt 0) {
                    # Reading every entry makes corrupt compressed payloads fail verification.
                }
            }
            finally {
                $stream.Dispose()
            }
        }

        $metadataHashes = @{}
        foreach ($entryName in @('extension.vsixmanifest', 'catalog.json', 'manifest.json')) {
            $entry = $archive.GetEntry($entryName)
            $stream = $entry.Open()
            $sha256 = [Security.Cryptography.SHA256]::Create()
            try {
                $metadataHashes[$entryName] = [BitConverter]::ToString(
                    $sha256.ComputeHash($stream)).Replace('-', '')
            }
            finally {
                $sha256.Dispose()
                $stream.Dispose()
            }
        }

        $manifestEntry = $archive.GetEntry('extension.vsixmanifest')
        $manifestReader = New-Object IO.StreamReader($manifestEntry.Open())
        try {
            [xml] $manifest = $manifestReader.ReadToEnd()
        }
        finally {
            $manifestReader.Dispose()
        }

        $namespaceManager = New-Object Xml.XmlNamespaceManager($manifest.NameTable)
        $namespaceManager.AddNamespace('vsix', 'http://schemas.microsoft.com/developer/vsx-schema/2011')
        $identity = $manifest.SelectSingleNode(
            '/vsix:PackageManifest/vsix:Metadata/vsix:Identity',
            $namespaceManager)
        if ($null -eq $identity -or
            [string]::IsNullOrWhiteSpace($identity.Id) -or
            [string]::IsNullOrWhiteSpace($identity.Version)) {
            throw 'The root extension.vsixmanifest does not contain a valid VSIX identity.'
        }

        return [PSCustomObject] @{
            EntryNames = $entryNames
            IdentityId = [string] $identity.Id
            IdentityVersion = [string] $identity.Version
            MetadataHashes = $metadataHashes
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Test-IsSignatureEntry {
    param(
        [Parameter(Mandatory = $true)]
        [string] $EntryName
    )

    return $EntryName -ceq '_rels/.rels' `
        -or $EntryName.StartsWith(
            'package/services/digital-signature/',
            [StringComparison]::Ordinal)
}

$signedPath = [IO.Path]::GetFullPath($VsixPath)
$unsignedPath = [IO.Path]::GetFullPath($UnsignedVsixPath)
foreach ($path in @($signedPath, $unsignedPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "VSIX file was not found: $path"
    }
}

$unsignedHash = (Get-FileHash -LiteralPath $unsignedPath -Algorithm SHA256).Hash
$signedHash = (Get-FileHash -LiteralPath $signedPath -Algorithm SHA256).Hash
if ($unsignedHash -eq $signedHash) {
    throw 'The signed VSIX is byte-for-byte identical to the unsigned input.'
}

$unsignedStructure = Get-VsixStructure -Path $unsignedPath
$signedStructure = Get-VsixStructure -Path $signedPath
if ($signedStructure.IdentityId -cne $unsignedStructure.IdentityId -or
    $signedStructure.IdentityVersion -cne $unsignedStructure.IdentityVersion) {
    throw 'The signed VSIX identity or version differs from the unsigned build artifact.'
}

$unsignedEntrySet = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
foreach ($entryName in $unsignedStructure.EntryNames) {
    $null = $unsignedEntrySet.Add($entryName)
}

$signedPayloadEntrySet = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
foreach ($entryName in $signedStructure.EntryNames) {
    if (-not (Test-IsSignatureEntry -EntryName $entryName)) {
        $null = $signedPayloadEntrySet.Add($entryName)
    }
}

$missingPayloadEntries = @(
    $unsignedEntrySet |
        Where-Object { -not $signedPayloadEntrySet.Contains($_) }
)
$unexpectedPayloadEntries = @(
    $signedPayloadEntrySet |
        Where-Object { -not $unsignedEntrySet.Contains($_) }
)
if ($missingPayloadEntries.Count -gt 0) {
    throw "Entries were removed while signing the VSIX: $($missingPayloadEntries -join ', ')."
}
if ($unexpectedPayloadEntries.Count -gt 0) {
    throw "Unexpected entries were added to the signed VSIX payload: $($unexpectedPayloadEntries -join ', ')."
}

foreach ($entryName in @('extension.vsixmanifest', 'catalog.json', 'manifest.json')) {
    if ($signedStructure.MetadataHashes[$entryName] -cne $unsignedStructure.MetadataHashes[$entryName]) {
        throw "VSIX metadata entry '$entryName' changed while signing."
    }
}

Add-Type -AssemblyName WindowsBase
$package = [IO.Packaging.Package]::Open(
    $signedPath,
    [IO.FileMode]::Open,
    [IO.FileAccess]::Read,
    [IO.FileShare]::Read)

try {
    $manager = New-Object IO.Packaging.PackageDigitalSignatureManager($package)
    if (-not $manager.IsSigned) {
        throw 'The VSIX does not contain an OPC package signature.'
    }

    $signatures = @($manager.Signatures)
    if ($signatures.Count -ne 1) {
        throw "Expected exactly one VSIX package signature; found $($signatures.Count)."
    }

    $verifyResult = $manager.VerifySignatures($false)
    if ($verifyResult -ne [IO.Packaging.VerifyResult]::Success) {
        throw "VSIX package signature validation failed: $verifyResult."
    }

    $signature = $signatures[0]
    $chainResult = [IO.Packaging.PackageDigitalSignatureManager]::VerifyCertificate($signature.Signer)
    if ($chainResult -ne [Security.Cryptography.X509Certificates.X509ChainStatusFlags]::NoError) {
        throw "VSIX signer certificate validation failed: $chainResult."
    }

    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        $signerFingerprint = [BitConverter]::ToString(
            $sha256.ComputeHash($signature.Signer.RawData)).Replace('-', '')
    }
    finally {
        $sha256.Dispose()
    }

    if ($signerFingerprint -ne $CertificateFingerprint.ToUpperInvariant()) {
        throw "The VSIX signer fingerprint does not match the configured Certum certificate."
    }

    Write-Output "Verified signed VSIX: $signedPath"
    Write-Output "Identity: $($signedStructure.IdentityId) $($signedStructure.IdentityVersion)"
    Write-Output "Entries: $($signedStructure.EntryNames.Count)"
    Write-Output "Signer: $($signature.Signer.Subject)"
    Write-Output "SHA-256: $signedHash"
}
finally {
    $package.Close()
}
