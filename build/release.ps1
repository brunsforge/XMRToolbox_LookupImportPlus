<#
.SYNOPSIS
  One-command release: set the version everywhere, build, assemble the drop-in
  folder + zip, and produce the store .nupkg. Does NOT push (you push with your
  own nuget.org API key — see docs\PUBLISHING.md).

.EXAMPLE
  build\release.ps1 0.1.1
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.\-]+)?$') {
    throw "Version '$Version' is not valid SemVer (e.g. 0.1.1 or 1.0.0-beta1)."
}

$root  = Split-Path -Parent $PSScriptRoot
$props = Join-Path $root "Directory.Build.props"

Write-Host "==> Setting version to $Version in Directory.Build.props" -ForegroundColor Cyan
$content = Get-Content $props -Raw
if (-not [System.Text.RegularExpressions.Regex]::IsMatch($content, '<Version>[^<]*</Version>')) {
    throw "Could not find a <Version> element in $props"
}
$updated = [System.Text.RegularExpressions.Regex]::Replace(
    $content, '<Version>[^<]*</Version>', "<Version>$Version</Version>")
Set-Content -Path $props -Value $updated -Encoding UTF8 -NoNewline

# 1) Build + assemble the non-host closure into deploy\Plugins (+ zip).
& (Join-Path $PSScriptRoot "package.ps1")

# 2) Produce the store .nupkg from that closure.
Write-Host "==> Packing store .nupkg ..." -ForegroundColor Cyan
$deploy = Join-Path $root "deploy"
& dotnet pack (Join-Path $PSScriptRoot "pack\LookupImportPlus.Pack.csproj") -c Release -o $deploy | Out-Null
if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed." }

$nupkg = Join-Path $deploy "LookupImportPlus.$Version.nupkg"
Write-Host ""
Write-Host "==> Release $Version ready." -ForegroundColor Green
Write-Host "    $nupkg"
Write-Host ""
Write-Host "Publish to the XrmToolBox store (nuget.org) with YOUR api key:" -ForegroundColor Yellow
Write-Host "  dotnet nuget push `"$nupkg`" -s https://api.nuget.org/v3/index.json -k <API_KEY>"
Write-Host ""
Write-Host "Remember to commit the version bump and tag the release, e.g.:"
Write-Host "  git commit -am `"Release $Version`"  &&  git tag v$Version"
