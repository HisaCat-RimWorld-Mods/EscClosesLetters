#Requires -Version 5.1
<#
.SYNOPSIS
  Sets a new mod version in Directory.Build.props, About.xml, and CHANGELOG.md.

  The current version is read from the files, so it does not have to be 1.0.0.
  Notes currently under Unreleased become the new version's notes.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Update-FileText {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [scriptblock] $Transform
    )

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $preamble = [System.Text.UTF8Encoding]::new($true).GetPreamble()
    $hasBom = $bytes.Length -ge $preamble.Length
    for ($i = 0; $hasBom -and $i -lt $preamble.Length; $i++) {
        if ($bytes[$i] -ne $preamble[$i]) {
            $hasBom = $false
        }
    }

    $encoding = [System.Text.UTF8Encoding]::new($hasBom)
    $offset = 0
    if ($hasBom) {
        $offset = $preamble.Length
    }
    $text = $encoding.GetString($bytes, $offset, $bytes.Length - $offset)
    $updated = & $Transform $text
    if ($updated -ne $text) {
        [System.IO.File]::WriteAllText($Path, $updated, $encoding)
    }
}

function Get-Newline {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Text
    )

    if ($Text.Contains("`r`n")) {
        return "`r`n"
    }
    return "`n"
}

function Update-ElementValue {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Text,

        [Parameter(Mandatory = $true)]
        [string] $ElementName,

        [Parameter(Mandatory = $true)]
        [string] $Value
    )

    $pattern = '<{0}>([^<]*)</{0}>' -f [regex]::Escape($ElementName)
    $regex = [regex]::new($pattern)
    $match = $regex.Match($Text)
    if (-not $match.Success) {
        throw "Element was not found: $ElementName"
    }

    $replacement = '<{0}>{1}</{0}>' -f $ElementName, $Value
    return $regex.Replace($Text, $replacement, 1)
}

function Update-Changelog {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Text,

        [Parameter(Mandatory = $true)]
        [string] $Version,

        [Parameter(Mandatory = $true)]
        [string] $Date
    )

    $newline = Get-Newline $Text
    $heading = '## [Unreleased]'
    $headingIndex = $Text.IndexOf($heading)
    if ($headingIndex -lt 0) {
        throw 'CHANGELOG.md is missing ## [Unreleased].'
    }
    if ($Text -match ('(?m)^## \[{0}\]' -f [regex]::Escape($Version))) {
        throw "CHANGELOG.md already contains version $Version."
    }

    $afterHeading = $headingIndex + $heading.Length
    $rest = $Text.Substring($afterHeading)
    $nextHeading = [regex]::Match($rest, '(?m)^## \[')
    if (-not $nextHeading.Success) {
        throw 'CHANGELOG.md is missing a previous version section.'
    }

    $notes = $rest.Substring(0, $nextHeading.Index).Trim()
    $tail = $rest.Substring($nextHeading.Index)
    $section = @(
        $heading
        ''
        "## [$Version] - $Date"
    ) -join $newline
    if (-not [string]::IsNullOrWhiteSpace($notes)) {
        $section += $newline + $newline + $notes
    }
    $section += $newline + $newline

    $updated = $Text.Substring(0, $headingIndex) + $section + $tail

    $comparePattern = '(?m)^(\[unreleased\]:[^\r\n]*?compare/)[vV][0-9A-Za-z\.\-+]+(\.\.\.HEAD[^\r\n]*)\r?$'
    $compareRegex = [regex]::new($comparePattern)
    if (-not $compareRegex.IsMatch($updated)) {
        throw 'CHANGELOG.md is missing the Unreleased compare link.'
    }
    $updated = $compareRegex.Replace($updated, {
            param($match)
            return $match.Groups[1].Value + 'v' + $Version + $match.Groups[2].Value
        }, 1)

    $linkMatch = [regex]::Match($updated, '(?m)^\[unreleased\]:[^\r\n]*\r?\n\[(?<version>[^\]]+)\]:\s*(?<url>\S+)')
    if (-not $linkMatch.Success) {
        throw 'CHANGELOG.md is missing a previous version link.'
    }

    $previousVersion = $linkMatch.Groups['version'].Value
    $previousUrl = $linkMatch.Groups['url'].Value
    $urlPattern = 'v{0}(?=[^\d]|$)' -f [regex]::Escape($previousVersion)
    $newUrl = [regex]::Replace($previousUrl, $urlPattern, ('v{0}' -f $Version), 1)
    if ($newUrl -eq $previousUrl) {
        throw "Could not build the link for version $Version from $previousUrl"
    }

    $unreleasedLine = [regex]::Match($updated, '(?m)^\[unreleased\]:[^\r\n]*')
    $insertAt = $unreleasedLine.Index + $unreleasedLine.Length
    $linkLine = $newline + ('[{0}]: {1}' -f $Version, $newUrl)
    return $updated.Insert($insertAt, $linkLine)
}

$Root = $PSScriptRoot
$propsPath = Join-Path $Root 'Directory.Build.props'
$aboutPath = Join-Path $Root 'About\About.xml'
$changelogPath = Join-Path $Root 'CHANGELOG.md'

foreach ($path in @($propsPath, $aboutPath, $changelogPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "File not found: $path"
    }
}

$version = (Read-Host 'Version').Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host 'Version must look like 0.0.0.' -ForegroundColor Red
    exit 1
}

$propsText = [System.IO.File]::ReadAllText($propsPath)
$currentMatch = [regex]::Match($propsText, '<ModVersion>([^<]*)</ModVersion>')
if (-not $currentMatch.Success) {
    throw 'Directory.Build.props is missing ModVersion.'
}
$currentVersion = $currentMatch.Groups[1].Value.Trim()
if ($currentVersion -eq $version) {
    throw "Version is already $version."
}

$date = Get-Date -Format 'yyyy-MM-dd'
$aboutText = [System.IO.File]::ReadAllText($aboutPath)
$changelogText = [System.IO.File]::ReadAllText($changelogPath)
$newPropsText = Update-ElementValue -Text $propsText -ElementName 'ModVersion' -Value $version
$newAboutText = Update-ElementValue -Text $aboutText -ElementName 'modVersion' -Value $version
$newChangelogText = Update-Changelog -Text $changelogText -Version $version -Date $date

Update-FileText -Path $propsPath -Transform {
    param($text)
    return $newPropsText
}

Update-FileText -Path $aboutPath -Transform {
    param($text)
    return $newAboutText
}

Update-FileText -Path $changelogPath -Transform {
    param($text)
    return $newChangelogText
}

Write-Host "Version updated: $currentVersion -> $version ($date)" -ForegroundColor Green
