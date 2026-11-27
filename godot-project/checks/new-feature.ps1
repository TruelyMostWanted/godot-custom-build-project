<#
.SYNOPSIS
    Creates a feature folder from _feature-template.
.DESCRIPTION
    The name is the wiki guide of the feature in kebab case: Audio-Bus-Settings -> audio-bus-settings.
    Replaces __FEATURE_PASCAL__ (C# namespace), __FEATURE_TITLE__ and __FEATURE_WIKI__ in the copied files.
.EXAMPLE
    ./godot-project/checks/new-feature.ps1 -Name audio-bus-settings
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-z][a-z0-9]*(-[a-z0-9]+)*$')]
    [string]$Name
)

$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
$target = Join-Path $projectDir $Name
if (Test-Path $target) { throw "Folder already exists: $target" }

$words = $Name -split '-' | ForEach-Object { $_.Substring(0, 1).ToUpper() + $_.Substring(1) }
$pascal = $words -join ''
$wiki = $words -join '-'
$title = $words -join ' '

Copy-Item -Path (Join-Path $projectDir '_feature-template') -Destination $target -Recurse

# UIDs must be unique per project: Godot creates new ones on import
Get-ChildItem -Path $target -Recurse -File -Filter *.uid | Remove-Item

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
Get-ChildItem -Path $target -Recurse -File -Include *.gd, *.cs, *.tscn | ForEach-Object {
    $text = [System.IO.File]::ReadAllText($_.FullName)
    $text = $text -replace ' uid="uid://[a-z0-9]+"', ''
    $text = $text.Replace('__FEATURE_PASCAL__', $pascal).Replace('__FEATURE_TITLE__', $title).Replace('__FEATURE_WIKI__', $wiki)
    $text = $text.Replace('res://_feature-template/', "res://$Name/")
    [System.IO.File]::WriteAllText($_.FullName, $text, $utf8NoBom)
}

Write-Host "Created $target (C# namespace TruelyInteractive.$pascal)" -ForegroundColor Green
Write-Host "Next: Example.gd/.cs from the wiki guide, Test.gd/.cs for the full API, then checks/run-checks.ps1 -Feature $Name"
