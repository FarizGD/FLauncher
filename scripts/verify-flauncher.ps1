param([Parameter(Mandatory=$true)][string]$Executable)
$ErrorActionPreference = 'Stop'
$info = New-Object System.Diagnostics.ProcessStartInfo
$info.FileName = (Resolve-Path -LiteralPath $Executable).Path
$info.Arguments = '--flauncher-diagnostics'
$info.UseShellExecute = $false
$info.CreateNoWindow = $true
$info.RedirectStandardOutput = $true
$info.RedirectStandardError = $true
$process = [Diagnostics.Process]::Start($info)
$stdout = $process.StandardOutput.ReadToEndAsync()
$stderr = $process.StandardError.ReadToEndAsync()
if (!$process.WaitForExit(15000)) { $process.Kill(); throw 'FLauncher diagnostics timed out.' }
if ($process.ExitCode -ne 0) { throw "FLauncher failed: $($stderr.Result)" }
$result = $stdout.Result | ConvertFrom-Json
if ($result.application -ne 'FLauncher' -or $result.externalLaunchers -ne $false -or $result.java -ne 'in-process Go service' -or $result.portable -ne $true) { throw 'Unexpected launcher build.' }
$expectedRoot = Join-Path (Split-Path -Parent $info.FileName) 'Flauncher'
if ($result.dataRoot -ne $expectedRoot) { throw 'Portable storage does not match executable location.' }
Write-Output 'Verified: one FLauncher executable, in-process Java and Bedrock services, permanent portable storage, no external launcher handoffs.'
