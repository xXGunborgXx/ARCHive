[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $projectRoot 'ARCHive.sln'
$projectPath = Join-Path $projectRoot 'src\ARCHive.App\ARCHive.App.csproj'
$publishPath = Join-Path $projectRoot 'artifacts\publish\win-x64'
$installerScript = Join-Path $projectRoot 'installer\ARCHive.iss'
$innoCompiler = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'

Push-Location $projectRoot
try {
    dotnet restore $solutionPath
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

    dotnet build $solutionPath --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

    dotnet test $solutionPath --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }

    dotnet restore $projectPath `
        --runtime win-x64
    if ($LASTEXITCODE -ne 0) { throw 'win-x64 publish restore failed.' }

    dotnet publish $projectPath `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $publishPath `
        --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

    if (-not (Test-Path -LiteralPath $innoCompiler)) {
        throw "Inno Setup Compiler was not found at $innoCompiler"
    }

    & $innoCompiler $installerScript
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
}
finally {
    Pop-Location
}
