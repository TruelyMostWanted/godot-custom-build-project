<#
.SYNOPSIS
    Creates a feature folder from _feature-template.
.DESCRIPTION
    Copies godot-project/_feature-template to godot-project/<Name>, replaces the placeholders
    __FEATURE_NAME__ (folder name) and __FEATURE_PASCAL__ (folder name in PascalCase) in file names and contents.
    The name should be the name of the feature branch in the engine, e.g. feature-resource-paths-server.
.EXAMPLE
    ./godot-project/checks/new-feature.ps1 -Name feature-resource-paths-server
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-z0-9]+(-[a-z0-9]+)*$')]
    [string]$Name
)

$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
$template = Join-Path $projectDir '_feature-template'
$target = Join-Path $projectDir $Name

if (Test-Path $target) { throw "Folder already exists: $target" }

$pascal = (($Name -split '-') | ForEach-Object { $_.Substring(0, 1).ToUpper() + $_.Substring(1) }) -join ''

Copy-Item -Path $template -Destination $target -Recurse

# rename files with the placeholder in their name
Get-ChildItem -Path $target -Recurse -File | Where-Object { $_.Name -like '*__FEATURE_PASCAL__*' } | ForEach-Object {
    Rename-Item -Path $_.FullName -NewName ($_.Name -replace '__FEATURE_PASCAL__', $pascal)
}

# replace placeholders and the template path in text files
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
Get-ChildItem -Path $target -Recurse -File -Include *.gd, *.cs, *.tscn, *.tres, *.md | ForEach-Object {
    $text = [System.IO.File]::ReadAllText($_.FullName)
    $text = $text.Replace('__FEATURE_PASCAL__', $pascal).Replace('__FEATURE_NAME__', $Name).Replace('res://_feature-template/', "res://$Name/")
    [System.IO.File]::WriteAllText($_.FullName, $text, $utf8NoBom)
}

Write-Host "Created $target (C# namespace TruelyInteractive.$pascal)" -ForegroundColor Green
Write-Host "Next: write the checks in scripts/gd/check.gd and scripts/cs/${pascal}Check.cs, then run checks/run-checks.ps1 -Feature $Name"
