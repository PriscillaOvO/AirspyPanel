param([string]$Compiler = $env:INNO_SETUP_COMPILER)
$ErrorActionPreference = 'Stop'
$version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid VERSION' }
if (-not $Compiler -or -not (Test-Path -LiteralPath $Compiler)) { throw 'Set INNO_SETUP_COMPILER or pass -Compiler with the full path to ISCC.exe. See CONTRIBUTING.md.' }
$release = Join-Path $PSScriptRoot "outputs\v$version"
if (Test-Path -LiteralPath $release) { throw "Release output exists; preserve old artifacts before rebuilding: $release" }
$stage = Join-Path $PSScriptRoot ("outputs\stage-" + [Guid]::NewGuid().ToString('N'))
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $stage
New-Item -ItemType Directory -Path (Join-Path $stage 'docs\guide'),$release -Force | Out-Null
foreach ($file in @('LICENSE','THIRD-PARTY-NOTICES.md')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $stage }
foreach ($lang in @('zh-CN','en-US','ja-JP','fr-FR','de-DE','ru-RU')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "docs\guide\$lang.md") -Destination (Join-Path $stage 'docs\guide')
}
$zip = Join-Path $release "AirspyPanel-v$version-portable.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
& $Compiler "/DAppVersion=$version" "/DStageDir=$stage" "/DReleaseDir=$release" (Join-Path $PSScriptRoot 'installer\AirspyPanel.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
$hashes = Get-ChildItem -LiteralPath $release -File | Where-Object Extension -in '.exe','.zip' | Sort-Object Name | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name }
[IO.File]::WriteAllText((Join-Path $release 'SHA256SUMS.txt'), ($hashes -join "`n") + "`n", (New-Object Text.UTF8Encoding($false)))
Write-Output "Release ready: $release"
