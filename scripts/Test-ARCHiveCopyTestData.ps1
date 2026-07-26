[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$TestDirectory,

    [switch]$Quick
)

$ErrorActionPreference = 'Stop'
$target = [System.IO.Path]::GetFullPath($TestDirectory)
$manifestPath = Join-Path $target 'ARCHive-test-manifest.json'

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Test manifest not found: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$results = foreach ($entry in $manifest.Files) {
    $path = Join-Path $target $entry.RelativePath
    $exists = Test-Path -LiteralPath $path -PathType Leaf
    $lengthMatches = $false
    $hashMatches = $null

    if ($exists) {
        $lengthMatches = (Get-Item -LiteralPath $path).Length -eq [long]$entry.Length
        if (-not $Quick -and $lengthMatches) {
            $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
            $hashMatches = $actualHash -eq [string]$entry.SHA256
        }
    }

    [pscustomobject]@{
        File = $entry.RelativePath
        Exists = $exists
        LengthMatches = $lengthMatches
        HashMatches = if ($Quick) { 'Not checked' } else { [bool]$hashMatches }
        Passed = $exists -and $lengthMatches -and ($Quick -or $hashMatches)
    }
}

$results | Format-Table -AutoSize
$failed = @($results | Where-Object { -not $_.Passed })

if ($failed.Count -gt 0) {
    throw "$($failed.Count) test file(s) failed verification."
}

$mode = if ($Quick) { 'size-only' } else { 'full SHA-256' }
Write-Host "PASS: $($results.Count) file(s) passed $mode verification."
