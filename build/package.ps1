<#
.SYNOPSIS
  Builds LookupImportPlus (Release) and assembles a drop-in XrmToolBox plugin
  folder + zip containing only the assemblies the host does NOT already provide.

.DESCRIPTION
  XrmToolBox ships the Dataverse SDK, Extensibility, McTools connection, and
  Newtonsoft.Json. We must ship our plugin plus the ClosedXML dependency closure
  and the net48 polyfills ClosedXML needs. Everything else in bin\Release is
  host-provided and is deliberately excluded.

  Output:
    deploy\Plugins\           drop these files into %AppData%\MscrmTools\XrmToolBox\Plugins
    deploy\LookupImportPlus-<version>.zip
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "src\LookupImportPlus\LookupImportPlus.csproj"
$binDir = Join-Path $root "src\LookupImportPlus\bin\$Configuration"
$deployDir = Join-Path $root "deploy"
$pluginsDir = Join-Path $deployDir "Plugins"

# Assemblies to ship. Prefixes match our plugin + ClosedXML closure + the net48
# polyfills that ClosedXML / DocumentFormat.OpenXml require (Span/Memory support).
$shipPrefixes = @(
    "LookupImportPlus",
    "ClosedXML",
    "DocumentFormat.OpenXml",
    "ExcelNumberFormat",
    "RBush",
    "SixLabors",
    "Microsoft.Bcl.HashCode",
    "System.Memory",
    "System.Buffers",
    "System.Numerics.Vectors",
    "System.Runtime.CompilerServices.Unsafe",
    "System.Threading.Tasks.Extensions",
    "System.ValueTuple",
    "System.IO.Packaging"
)

Write-Host "==> Building $Configuration ..." -ForegroundColor Cyan
& dotnet build $proj -c $Configuration | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$pluginDll = Join-Path $binDir "LookupImportPlus.dll"
if (-not (Test-Path $pluginDll)) { throw "Plugin DLL not found at $pluginDll" }
$version = [System.Reflection.AssemblyName]::GetAssemblyName($pluginDll).Version.ToString()

if (Test-Path $deployDir) { Remove-Item $deployDir -Recurse -Force }
New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null

Write-Host "==> Collecting shippable assemblies ..." -ForegroundColor Cyan
$copied = @()
Get-ChildItem $binDir -Filter *.dll | ForEach-Object {
    $name = $_.BaseName
    if ($shipPrefixes | Where-Object { $name -eq $_ -or $name.StartsWith("$_.") -or $name.StartsWith("$_") }) {
        Copy-Item $_.FullName -Destination $pluginsDir
        $copied += $_.Name
    }
}

$copied | Sort-Object | ForEach-Object { Write-Host "    + $_" }
Write-Host "    ($($copied.Count) assemblies)" -ForegroundColor DarkGray

$zip = Join-Path $deployDir "LookupImportPlus-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $pluginsDir "*") -DestinationPath $zip

Write-Host ""
Write-Host "==> Done. Version $version" -ForegroundColor Green
Write-Host "    Drop-in folder: $pluginsDir"
Write-Host "    Zip:            $zip"
Write-Host ""
Write-Host "Install: copy the files into" -ForegroundColor Yellow
Write-Host "  %AppData%\MscrmTools\XrmToolBox\Plugins" -ForegroundColor Yellow
Write-Host "then restart XrmToolBox."
