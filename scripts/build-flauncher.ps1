param([string]$GoPath = '', [switch]$SkipTests, [switch]$SkipArchive)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repo 'LeviLauncher'
$output = Join-Path $repo 'artifacts/FLauncher-native'
if (!$GoPath) {
    $goCommand = Get-Command go -ErrorAction SilentlyContinue
    if ($goCommand) { $GoPath = $goCommand.Source }
    else { $GoPath = Join-Path $repo '.cache/toolchains/go/bin/go.exe' }
}
if (!(Test-Path -LiteralPath $GoPath)) { throw 'Install Go 1.25.3 or newer, or pass -GoPath to go.exe.' }
$savedPath = $env:PATH
$env:PATH = (Split-Path -Parent $GoPath) + ';' + $env:PATH
function Assert-Command([string]$Name) { if ($LASTEXITCODE -ne 0) { throw "$Name failed (exit $LASTEXITCODE)." } }
try {
    Push-Location $source
    & $GoPath mod download
    Assert-Command 'Go dependencies'
    if (!(Test-Path frontend/dist/index.html)) {
        New-Item -ItemType Directory -Force frontend/dist | Out-Null
        Set-Content frontend/dist/index.html '<!doctype html><title>FLauncher build staging</title>'
    }
    $wails = Join-Path (& $GoPath env GOPATH) 'bin/wails3.exe'
    if (!(Test-Path -LiteralPath $wails)) { & $GoPath install github.com/wailsapp/wails/v3/cmd/wails3@v3.0.0-beta.19; Assert-Command 'Wails tooling' }
    & $wails generate bindings -clean=true
    Assert-Command 'Frontend bindings'
    if (!$SkipTests) {
        & $GoPath test ./internal/javaedition ./internal/apppath ./internal/nativeinstall ./internal/mcservice ./internal/versionlaunch ./internal/xbox ./internal/app ./internal/uwp ./internal/versions
        Assert-Command 'Go tests'
    }
    Push-Location frontend
    try {
        npm ci --no-audit --no-fund
        Assert-Command 'Frontend dependencies'
        npm run build
        Assert-Command 'Production frontend'
    } finally { Pop-Location }
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    & $wails generate syso -arch amd64 -icon build/windows/icon.ico -manifest build/windows/wails.exe.manifest -info build/windows/info.json -out flauncher_windows_amd64.syso
    Assert-Command 'Windows executable resources'
    try {
        & $GoPath build -tags production -trimpath -buildvcs=false -ldflags '-H windowsgui' -o (Join-Path $output 'FLauncher.exe') .
        Assert-Command 'FLauncher executable'
    } finally { if (Test-Path flauncher_windows_amd64.syso) { Remove-Item -LiteralPath flauncher_windows_amd64.syso } }
    Copy-Item -LiteralPath COPYING, THIRD_PARTY_NOTICES -Destination $output -Force
    Copy-Item -LiteralPath (Join-Path $repo 'NATIVE-LAUNCHER.md') -Destination (Join-Path $output 'README.md') -Force
    & (Join-Path $repo 'scripts/verify-flauncher.ps1') -Executable (Join-Path $output 'FLauncher.exe')
    if (!$SkipArchive) {
        Compress-Archive -LiteralPath @((Join-Path $output 'FLauncher.exe'), (Join-Path $output 'COPYING'), (Join-Path $output 'THIRD_PARTY_NOTICES'), (Join-Path $output 'README.md')) -DestinationPath (Join-Path $repo 'artifacts/FLauncher-native-win-x64.zip') -Force
        Write-Output 'Created artifacts/FLauncher-native-win-x64.zip'
    }
    Write-Output "Built $output/FLauncher.exe (Java + Bedrock in one process)."
} finally { Pop-Location; $env:PATH = $savedPath }
