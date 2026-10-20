param([string]$Version = '2.0.0')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid version' }
$output = Join-Path $PSScriptRoot 'outputs'
$stage = Join-Path $output ("package-" + [Guid]::NewGuid().ToString('N'))
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\使用说明.md') -Destination (Join-Path $stage '使用说明.txt')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination $stage
$zip = Join-Path $output "AirspyPanel-v$Version-windows.zip"
if (Test-Path -LiteralPath $zip) { throw "Already exists: $zip" }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS.txt'), "$hash  $([IO.Path]::GetFileName($zip))`n", (New-Object Text.UTF8Encoding($false)))
Write-Output $zip
