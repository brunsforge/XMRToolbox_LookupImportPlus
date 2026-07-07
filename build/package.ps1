<#
.SYNOPSIS
  Builds LookupImportPlus (Release) and produces a SINGLE merged plugin assembly
  (LookupImportPlus.dll) that contains its third-party dependencies.

.DESCRIPTION
  The XrmToolBox Tool Library requires every assembly in the package to carry the
  package version. Third-party libraries (ClosedXML 0.105, SixLabors.Fonts 1.0.0,
  ClosedXML.Parser 1.0.0, …) can't be re-versioned, so — like the canonical
  plugins (e.g. PluginTraceViewer ships a single Rappen.XTB.PTV.dll) — we ILRepack
  the dependency closure INTO LookupImportPlus.dll. Host/framework assemblies
  (Microsoft.Xrm.*, XrmToolBox.*, Newtonsoft.Json, System.*, Microsoft.Bcl.*) are
  provided by XrmToolBox and are neither merged nor shipped.

  Output:
    deploy\Plugins\LookupImportPlus.dll   drop into %AppData%\MscrmTools\XrmToolBox\Plugins
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
$toolsDir = Join-Path $PSScriptRoot "tools"

# Third-party assemblies to merge INTO the plugin. These are genuine libraries
# XrmToolBox does NOT provide, so they must live inside our single assembly:
#   • the ClosedXML closure (ClosedXML, OpenXml, SixLabors.Fonts, RBush, …)
#   • Microsoft.Bcl.HashCode — ClosedXML needs it; XrmToolBox ships neither the
#     assembly nor a binding redirect for it, so an external reference would fail
#     to load ("could not load Microsoft.Bcl.HashCode 1.0.0.0").
# NOT merged (host-provided WITH binding redirects in XrmToolBox.exe.config):
# System.Memory / System.Buffers / System.Numerics.Vectors /
# System.Runtime.CompilerServices.Unsafe — resolved by ILRepack via /lib, left
# external, and redirected to XrmToolBox's versions at runtime.
$mergeDlls = @(
    "ClosedXML.dll",
    "ClosedXML.Parser.dll",
    "DocumentFormat.OpenXml.dll",
    "DocumentFormat.OpenXml.Framework.dll",
    "ExcelNumberFormat.dll",
    "SixLabors.Fonts.dll",
    "RBush.dll",
    "Microsoft.Bcl.HashCode.dll"
    # NOTE: do NOT merge the System.* Span/Memory polyfills — internalizing them
    # breaks ClosedXML/OpenXml document generation (empty output). XrmToolBox ships
    # them and binding-redirects them, so they stay external and load at runtime.
)

function Get-ILRepack {
    $exe = Join-Path $toolsDir "ILRepack.exe"
    if (Test-Path $exe) { return $exe }
    Write-Host "==> Fetching ILRepack ..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $toolsDir -Force | Out-Null
    $ver = "2.0.45"
    # Expand-Archive only accepts a .zip extension, so download as .zip.
    $zip = Join-Path $env:TEMP "ilrepack.$ver.zip"
    Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/ilrepack/$ver/ilrepack.$ver.nupkg" -OutFile $zip
    $extract = Join-Path $env:TEMP "ilrepack.$ver"
    if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
    Expand-Archive -Path $zip -DestinationPath $extract
    Copy-Item (Join-Path $extract "tools\ILRepack.exe") $exe
    return $exe
}

Write-Host "==> Building $Configuration ..." -ForegroundColor Cyan
& dotnet build $proj -c $Configuration | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$pluginDll = Join-Path $binDir "LookupImportPlus.dll"
if (-not (Test-Path $pluginDll)) { throw "Plugin DLL not found at $pluginDll" }
$version = [System.Reflection.AssemblyName]::GetAssemblyName($pluginDll).Version.ToString()

if (Test-Path $deployDir) { Remove-Item $deployDir -Recurse -Force }
New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null

$ilrepack = Get-ILRepack
$outDll = Join-Path $pluginsDir "LookupImportPlus.dll"

# Primary assembly FIRST (keeps its identity/version and the plugin type).
$inputs = @($pluginDll) + ($mergeDlls | ForEach-Object { Join-Path $binDir $_ })
foreach ($i in $inputs) { if (-not (Test-Path $i)) { throw "Merge input missing: $i" } }

Write-Host "==> Merging $($mergeDlls.Count) dependencies into LookupImportPlus.dll ..." -ForegroundColor Cyan
& $ilrepack /out:$outDll /lib:$binDir /target:library /internalize @inputs | Out-Null
if ($LASTEXITCODE -ne 0) { throw "ILRepack failed." }

$merged = [System.Reflection.AssemblyName]::GetAssemblyName($outDll).Version.ToString()
$sizeMb = [Math]::Round((Get-Item $outDll).Length / 1MB, 1)
Write-Host "    LookupImportPlus.dll  v$merged  ($sizeMb MB, single assembly)" -ForegroundColor DarkGray

$zip = Join-Path $deployDir "LookupImportPlus-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $pluginsDir "*") -DestinationPath $zip

Write-Host ""
Write-Host "==> Done. Version $version" -ForegroundColor Green
Write-Host "    Drop-in folder: $pluginsDir"
Write-Host "    Zip:            $zip"
Write-Host ""
Write-Host "Install: copy LookupImportPlus.dll into" -ForegroundColor Yellow
Write-Host "  %AppData%\MscrmTools\XrmToolBox\Plugins" -ForegroundColor Yellow
Write-Host "then restart XrmToolBox."
