<#
.SYNOPSIS
    Builds the C# project and runs the tests and examples of the feature folders headless.
.DESCRIPTION
    A feature folder has scenes/test_gd.tscn, test_cs.tscn, example_gd.tscn and example_cs.tscn.
    A test passes with exit code 0. An example passes with exit code 0 and without "ERROR" in its output.
    Every scene must finish within -Timeout seconds (a GDScript parse error leaves the scene running).
    The engine must be a Mono build of truely-godot-engine.
.EXAMPLE
    ./godot-project/checks/run-checks.ps1 -Godot P:/Projects/GitHub/truelymostwanted-godot-engine/bin/godot.windows.editor.x86_64.mono.exe
.EXAMPLE
    ./godot-project/checks/run-checks.ps1 -Godot <path> -Feature seeds -Only test
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Godot,
    [string]$Feature = '',
    [ValidateSet('all', 'test', 'example')]
    [string]$Only = 'all',
    [int]$Timeout = 120,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path $Godot)) { throw "Godot executable not found: $Godot" }

# On Windows, the editor executable is a GUI program: PowerShell neither waits for it nor sets an exit code.
# Use the console wrapper next to it: local builds have godot.<...>.console.exe, the release zips Truely_Godot<...>_console.exe.
if ($Godot -match '\.exe$' -and $Godot -notmatch '[._]console\.exe$') {
    $candidates = @(($Godot -replace '\.exe$', '.console.exe'), ($Godot -replace '\.exe$', '_console.exe'))
    $console = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $console) { throw "Console executable not found next to the editor: $($candidates -join ' or ')" }
    $Godot = $console
}

if (-not $NoBuild) {
    Write-Host '==> use-engine' -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot 'use-engine.ps1') -Godot $Godot
    Write-Host '==> dotnet build' -ForegroundColor Cyan
    Push-Location $projectDir
    try {
        & dotnet build
        if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }
    }
    finally { Pop-Location }
}

# the editor imports the project once, so that script classes (FeatureTest) are known
Write-Host '==> import' -ForegroundColor Cyan
& $Godot --headless --path $projectDir --import | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Import failed with exit code $LASTEXITCODE" }

$folders = Get-ChildItem -Path $projectDir -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'scenes/test_gd.tscn') }
$folders = $folders | Where-Object { $_.Name -ne '_feature-template' }
if ($Feature) { $folders = $folders | Where-Object { $_.Name -eq $Feature } }
if (-not $folders) { Write-Host 'No feature folders found.' -ForegroundColor Yellow; exit 0 }

$kinds = if ($Only -eq 'all') { @('test', 'example') } else { @($Only) }
$log = Join-Path ([System.IO.Path]::GetTempPath()) "run-checks-$PID.log"
$results = @()
foreach ($folder in $folders) {
    foreach ($kind in $kinds) {
        foreach ($language in @('gd', 'cs')) {
            $scene = "res://$($folder.Name)/scenes/${kind}_$language.tscn"
            if (-not (Test-Path (Join-Path $folder.FullName "scenes/${kind}_$language.tscn"))) {
                $results += [pscustomobject]@{ Feature = $folder.Name; Kind = $kind; Language = $language; Result = 'MISSING' }
                continue
            }
            Write-Host "==> $scene" -ForegroundColor Cyan
            $process = Start-Process -FilePath $Godot -ArgumentList '--headless', '--path', "`"$projectDir`"", $scene `
                -NoNewWindow -PassThru -RedirectStandardOutput $log -RedirectStandardError "$log.err"
            $handle = $process.Handle  # keeps the exit code available after the process ended
            if (-not $process.WaitForExit($Timeout * 1000)) {
                $process.Kill()
                $result = 'TIMEOUT'
            }
            else {
                $output = (Get-Content $log, "$log.err" -Raw) -join "`n"
                Write-Host $output
                $result = 'PASSED'
                if ($process.ExitCode -ne 0) { $result = "FAILED ($($process.ExitCode))" }
                elseif ($kind -eq 'example' -and $output -cmatch '(?m)^(SCRIPT )?ERROR') { $result = 'FAILED (ERROR in output)' }
            }
            $results += [pscustomobject]@{ Feature = $folder.Name; Kind = $kind; Language = $language; Result = $result }
        }
    }
}

$results | Format-Table -AutoSize
if ($results | Where-Object { $_.Result -ne 'PASSED' }) { exit 1 }
exit 0
