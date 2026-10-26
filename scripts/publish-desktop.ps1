# Compatibility entry point. The Avalonia/bundled-launcher experiment is superseded.
param([string]$GoPath = '', [switch]$SkipTests, [switch]$SkipArchive)
& (Join-Path $PSScriptRoot 'build-flauncher.ps1') @PSBoundParameters
