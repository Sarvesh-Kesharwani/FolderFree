param(
  [switch]$SkipAssets
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$dist = Join-Path $root "dist"
$assets = Join-Path $root "assets"
$source = Join-Path $root "src\Program.cs"
$manifest = Join-Path $root "app.manifest"
$icon = Join-Path $assets "FolderFree.ico"
$out = Join-Path $dist "FolderFree.exe"
$rootOut = Join-Path $root "FolderFree.exe"
$release = Join-Path $root "Release"
$releaseOut = Join-Path $release "FolderFree.exe"

New-Item -ItemType Directory -Force -Path $dist, $assets, $release | Out-Null

if (-not $SkipAssets) {
  & (Join-Path $root "tools\GenerateAssets.ps1")
}

$compilerCandidates = @(
  "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe",
  "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
)

$csc = $compilerCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
  throw "Could not find csc.exe. Install Visual Studio Build Tools or .NET Framework compiler."
}

& $csc `
  /nologo `
  /target:winexe `
  /platform:x64 `
  /optimize+ `
  /win32icon:$icon `
  /win32manifest:$manifest `
  /out:$out `
  /reference:System.dll `
  /reference:System.Core.dll `
  /reference:System.Drawing.dll `
  /reference:System.Windows.Forms.dll `
  $source

Copy-Item -LiteralPath $out -Destination $releaseOut -Force

try {
  Copy-Item -LiteralPath $out -Destination $rootOut -Force
} catch {
  Write-Warning "Could not update $rootOut because it is currently running. Close that window and rerun this build if you need the root copy refreshed."
}

Write-Host "Built GUI app: $releaseOut"
