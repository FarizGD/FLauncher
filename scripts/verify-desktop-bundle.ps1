param([Parameter(Mandatory=$true)][string]$BundlePath)
$ErrorActionPreference = 'Stop'
$bundle = (Resolve-Path -LiteralPath $BundlePath).Path
$manifest = Get-Content -LiteralPath (Join-Path $bundle 'backends.json') -Raw | ConvertFrom-Json
$required = @('FLauncher.Desktop.exe', 'hostfxr.dll', 'coreclr.dll', 'backends/bedrock/LeviLauncher.exe', 'backends/java/Qt6Core.dll', 'backends/java/Qt6Widgets.dll', 'backends/java/platforms/qwindows.dll', 'backends/java/jars/NewLaunch.jar', 'sources/FLauncher-desktop-source.zip', 'sources/prism-11.1.1-source.tar.gz', 'sources/levi-0.3.13-source.tar.gz', 'LICENSE', 'COPYING.md')
foreach ($relative in $required) {
    if (!(Test-Path -LiteralPath (Join-Path $bundle $relative) -PathType Leaf)) { throw "Incomplete desktop bundle: missing $relative" }
}
$engineName = if ($manifest.prism.customBuild) { 'FLauncher.exe' } else { 'prismlauncher.exe' }
$engine = Join-Path $bundle "backends/java/$engineName"
if (!(Test-Path -LiteralPath $engine)) { throw "Missing Java engine: $engineName" }
if ((Get-FileHash -LiteralPath (Join-Path $bundle 'backends/bedrock/LeviLauncher.exe')).Hash -ine $manifest.levi.sha256) { throw 'Bedrock backend checksum mismatch.' }
# --version exercises the Java engine and its DLL loading without launching a game or changing account data.
$info = New-Object System.Diagnostics.ProcessStartInfo
$info.FileName = $engine
$info.Arguments = '--version'
$info.WorkingDirectory = Split-Path -Parent $engine
$info.UseShellExecute = $false
$info.CreateNoWindow = $true
$info.RedirectStandardOutput = $true
$info.RedirectStandardError = $true
$process = [System.Diagnostics.Process]::Start($info)
$stdout = $process.StandardOutput.ReadToEndAsync()
$stderr = $process.StandardError.ReadToEndAsync()
if (!$process.WaitForExit(30000)) { $process.Kill(); throw 'Java backend startup timed out.' }
if ($process.ExitCode -ne 0) { throw "Java backend failed to start: $($stderr.Result)" }
Write-Output "Verified bundle: Java $($manifest.prism.version), Bedrock $($manifest.levi.version), .NET runtime and native dependencies."
