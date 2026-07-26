[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputDirectory,

    [ValidateRange(0.01, 1024)]
    [double[]]$FileSizeGB = @(1),

    [ValidateRange(1, 64)]
    [int]$FilesPerSize = 1,

    [int]$Seed = 20260726
)

$ErrorActionPreference = 'Stop'
$target = [System.IO.Path]::GetFullPath($OutputDirectory)
$root = [System.IO.Path]::GetPathRoot($target)

if ([string]::IsNullOrWhiteSpace($root) -or $target -eq $root) {
    throw 'Choose a dedicated test-data folder, not a drive root.'
}

if (Test-Path -LiteralPath $target) {
    $existing = Get-ChildItem -LiteralPath $target -Force
    if ($existing.Count -gt 0) {
        throw "The test-data folder must be empty: $target"
    }
}
else {
    New-Item -ItemType Directory -Path $target | Out-Null
}

$sizes = foreach ($size in $FileSizeGB) {
    [long][Math]::Floor($size * 1GB)
}
$requiredBytes = [long](($sizes | Measure-Object -Sum).Sum * $FilesPerSize)
$drive = [System.IO.DriveInfo]::new($root)
$reserveBytes = [long][Math]::Max(1GB, $requiredBytes * 0.05)

if ($drive.AvailableFreeSpace -lt ($requiredBytes + $reserveBytes)) {
    throw "Insufficient free space. The test set needs $requiredBytes bytes plus a safety reserve."
}

$payloadDirectory = Join-Path $target 'payload'
New-Item -ItemType Directory -Path $payloadDirectory | Out-Null
$manifestEntries = [System.Collections.Generic.List[object]]::new()
$buffer = [byte[]]::new(4MB)
$fileNumber = 0

try {
    foreach ($size in $sizes) {
        for ($copy = 1; $copy -le $FilesPerSize; $copy++) {
            $fileNumber++
            $sizeLabel = ('{0:0.##}' -f ($size / 1GB)).Replace('.', 'p')
            $fileName = 'ARCHive-test-{0:D3}-{1}GB.bin' -f $fileNumber, $sizeLabel
            $filePath = Join-Path $payloadDirectory $fileName
            $relativePath = Join-Path 'payload' $fileName
            $random = [System.Random]::new($Seed + $fileNumber)
            $hash = [System.Security.Cryptography.IncrementalHash]::CreateHash(
                [System.Security.Cryptography.HashAlgorithmName]::SHA256)
            $stream = [System.IO.FileStream]::new(
                $filePath,
                [System.IO.FileMode]::CreateNew,
                [System.IO.FileAccess]::Write,
                [System.IO.FileShare]::None,
                $buffer.Length,
                [System.IO.FileOptions]::SequentialScan)
            $written = 0L

            try {
                while ($written -lt $size) {
                    $random.NextBytes($buffer)
                    $count = [int][Math]::Min($buffer.Length, $size - $written)
                    $stream.Write($buffer, 0, $count)
                    $hash.AppendData($buffer, 0, $count)
                    $written += $count
                    $percent = if ($size -eq 0) { 100 } else { $written * 100 / $size }
                    Write-Progress `
                        -Activity "Creating physical test file $fileNumber" `
                        -Status "$fileName - $([Math]::Floor($percent))%" `
                        -PercentComplete $percent
                }

                $stream.Flush($true)
            }
            finally {
                $stream.Dispose()
            }

            $hashText = [Convert]::ToHexString($hash.GetHashAndReset())
            $hash.Dispose()
            $attributes = [System.IO.File]::GetAttributes($filePath)

            $manifestEntries.Add([pscustomobject]@{
                RelativePath = $relativePath
                Length = $size
                SHA256 = $hashText
                IsSparse = [bool]($attributes -band [System.IO.FileAttributes]::SparseFile)
            })
        }
    }
}
finally {
    Write-Progress -Activity 'Creating physical test data' -Completed
}

$manifest = [pscustomobject]@{
    SchemaVersion = 1
    Generator = 'New-ARCHiveCopyTestData.ps1'
    CreatedAt = [DateTimeOffset]::Now.ToString('O')
    Seed = $Seed
    TotalBytes = $requiredBytes
    FileCount = $manifestEntries.Count
    Files = $manifestEntries
}
$manifestPath = Join-Path $target 'ARCHive-test-manifest.json'
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8

Write-Host "Created $($manifestEntries.Count) physically written test file(s)."
Write-Host "Manifest: $manifestPath"
Write-Host 'Use Test-ARCHiveCopyTestData.ps1 on the completed copy to verify every hash.'
