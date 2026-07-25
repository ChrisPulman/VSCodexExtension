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
    Write-Output "Signer: $($signature.Signer.Subject)"
    Write-Output "SHA-256: $signedHash"
}
finally {
    $package.Close()
}
