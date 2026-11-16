#Requires -Version 5.1
<#
.SYNOPSIS
  Reads About/About.xml <supportedVersions> and builds the solution once per version.

.PARAMETER Configuration
  MSBuild configuration (Debug or Release). Release is typical before publish.
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root = $PSScriptRoot
$modDirName = Split-Path $Root -Leaf
$AboutPath = Join-Path $Root 'About\About.xml'
$Sln = Join-Path $Root "Source\$modDirName.sln"

if (-not (Test-Path -LiteralPath $AboutPath)) {
    throw "About.xml not found: $AboutPath"
}
if (-not (Test-Path -LiteralPath $Sln)) {
    throw "Solution not found: $Sln"
}

[xml] $aboutDoc = Get-Content -LiteralPath $AboutPath -Raw -Encoding UTF8
$supported = $aboutDoc.ModMetaData.supportedVersions
if ($null -eq $supported) {
    throw 'About.xml: <supportedVersions> not found.'
}

$versions = [System.Collections.Generic.List[string]]::new()
foreach ($li in @($supported.li)) {
    $text = if ($li -is [string]) { $li } else { $li.InnerText }
    $v = $text.Trim()
    if ($v) {
        $versions.Add($v)
    }
}

if ($versions.Count -eq 0) {
    throw 'About.xml: <supportedVersions> has no <li> entries.'
}

Write-Host "Build All: RimWorld targets from About.xml: $($versions -join ', ')" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration`n" -ForegroundColor Cyan

foreach ($ver in $versions) {
    Write-Host "=== dotnet build (TargetRimWorldVersion=$ver) ===" -ForegroundColor Yellow
    $buildArgs = @(
        'build',
        $Sln,
        '-c', $Configuration,
        "/p:RimWorldVersionOverride=$ver",
        '/p:GenerateFullPaths=true',
        '/consoleloggerparameters:NoSummary;ForceNoAlign'
    )
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed for RimWorld $ver (exit code $LASTEXITCODE)."
    }
}

Write-Host "`nDone." -ForegroundColor Green
