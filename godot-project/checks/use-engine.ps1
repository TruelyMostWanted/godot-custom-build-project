<#
.SYNOPSIS
    Makes the project use the .NET packages of an engine build: copies them to nuget-local and sets the SDK version.
.DESCRIPTION
    Every Mono engine (release zip or local build) carries its packages in GodotSharp/Tools/nupkgs next to the
    executable: Godot.NET.Sdk, Godot.SourceGenerators, GodotSharp and GodotSharpEditor. nuget.config takes these
    packages only from ../nuget-local (P:/Projects/GitHub/nuget-local), so a new engine version (e.g. 4.8.0-beta.5
    after 4.8.0-beta) cannot build until its packages are there. This script
      1. copies the packages of the engine to nuget-local,
      2. removes cached copies of the same version from the NuGet cache when a package changed,
      3. sets Sdk="Godot.NET.Sdk/<version>" in GodotCustomBuildProject.csproj.
    run-checks.ps1 calls it before the build.
.EXAMPLE
    ./godot-project/checks/use-engine.ps1 -Godot "P:/Programs/Game Engines/Truely Godot Engine/Truely_Godot4.8-beta5_3bd081f_mono_win64/Truely_Godot4.8-beta5_3bd081f_mono_win64.exe"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Godot
)

$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
$feed = [IO.Path]::GetFullPath((Join-Path $projectDir '../../nuget-local'))
$csproj = Join-Path $projectDir 'GodotCustomBuildProject.csproj'

$engineDir = if (Test-Path $Godot -PathType Container) { $Godot } else { Split-Path -Parent $Godot }
$nupkgs = Join-Path $engineDir 'GodotSharp/Tools/nupkgs'
if (-not (Test-Path $nupkgs)) { throw "No GodotSharp/Tools/nupkgs next to the engine: $engineDir (not a Mono build?)" }

$sdk = Get-ChildItem $nupkgs -Filter 'Godot.NET.Sdk.*.nupkg' | Sort-Object LastWriteTime -Descending
if ($sdk.Count -ne 1) { throw "Expected exactly one Godot.NET.Sdk package in ${nupkgs}, found $($sdk.Count): $($sdk.Name -join ', ')" }
$version = $sdk[0].Name -replace '^Godot\.NET\.Sdk\.(.+)\.nupkg$', '$1'
Write-Host "Engine packages $version from $nupkgs" -ForegroundColor Cyan

New-Item -ItemType Directory -Force $feed | Out-Null
$cache = Join-Path $env:USERPROFILE '.nuget/packages'
foreach ($file in Get-ChildItem $nupkgs -File | Where-Object { $_.Name -match "\.$([regex]::Escape($version))\.s?nupkg$" }) {
    $target = Join-Path $feed $file.Name
    if (Test-Path $target) {
        if ((Get-FileHash $target).Hash -eq (Get-FileHash $file.FullName).Hash) { continue }
        # Same version, other content: NuGet would keep using the old copy from its cache.
        $id = $file.Name -replace "\.$([regex]::Escape($version))\.s?nupkg$", ''
        $cached = Join-Path $cache "$($id.ToLowerInvariant())/$($version.ToLowerInvariant())"
        if (Test-Path $cached) { Remove-Item -Recurse -Force $cached; Write-Host "  removed cached $id $version" }
    }
    Copy-Item $file.FullName $target -Force
    Write-Host "  copied $($file.Name)"
}

$text = [IO.File]::ReadAllText($csproj)
$updated = [regex]::Replace($text, 'Sdk="Godot\.NET\.Sdk/[^"]+"', "Sdk=`"Godot.NET.Sdk/$version`"")
if ($updated -ne $text) {
    [IO.File]::WriteAllText($csproj, $updated)
    Write-Host "  GodotCustomBuildProject.csproj now uses Godot.NET.Sdk/$version"
}
