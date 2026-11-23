<#
.SYNOPSIS
    Builds the C# project and runs the feature checks headless.
.DESCRIPTION
    Runs scenes/check_gd.tscn and scenes/check_cs.tscn of every feature folder (or of one feature with -Feature).
    Each check exits with 0 (passed) or 1 (failed). The script exits with 1 when any check failed.
    The engine must be a Mono build of truely-godot-engine.
.EXAMPLE
    ./godot-project/checks/run-checks.ps1 -Godot P:/Projects/GitHub/truelymostwanted-godot-engine/bin/godot.windows.editor.x86_64.mono.exe
.EXAMPLE
    ./godot-project/checks/run-checks.ps1 -Godot <path> -Feature 03-feature-path-prefix-server
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Godot,
    [string]$Feature = '',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path $Godot)) { throw "Godot executable not found: $Godot" }

if (-not $NoBuild) {
    Write-Host '==> dotnet build' -ForegroundColor Cyan
    Push-Location $projectDir
    try {
        & dotnet build
        if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }
    }
    finally { Pop-Location }
}

# the editor imports the project once, so that script classes (FeatureCheck) are known
Write-Host '==> import' -ForegroundColor Cyan
& $Godot --headless --path $projectDir --import | Out-Null

$folders = Get-ChildItem -Path $projectDir -Directory | Where-Object { $_.Name -match '^[0-9]{2,}-' }
if ($Feature) { $folders = $folders | Where-Object { $_.Name -eq $Feature } }
if (-not $folders) { Write-Host 'No feature folders found.' -ForegroundColor Yellow; exit 0 }

$results = @()
foreach ($folder in $folders) {
    foreach ($language in @('gd', 'cs')) {
        $scene = "res://$($folder.Name)/scenes/check_$language.tscn"
        if (-not (Test-Path (Join-Path $folder.FullName "scenes/check_$language.tscn"))) {
            $results += [pscustomobject]@{ Feature = $folder.Name; Language = $language; Result = 'MISSING' }
            continue
        }
        Write-Host "==> $scene" -ForegroundColor Cyan
        & $Godot --headless --path $projectDir $scene
        $result = if ($LASTEXITCODE -eq 0) { 'PASSED' } else { 'FAILED' }
        $results += [pscustomobject]@{ Feature = $folder.Name; Language = $language; Result = $result }
    }
}

$results | Format-Table -AutoSize
if ($results | Where-Object { $_.Result -ne 'PASSED' }) { exit 1 }
exit 0
