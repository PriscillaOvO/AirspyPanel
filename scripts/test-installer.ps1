param([Parameter(Mandatory=$true)][string]$Installer)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$registry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{6613E914-CC1E-4C25-A952-992BDB8500F3}_is1'
if (Test-Path $registry) { throw 'An installed copy already exists. Use an isolated Windows account to run installer tests.' }
$target = Join-Path $root ('outputs\installer-test\' + [Guid]::NewGuid().ToString('N') + '\Unicode 安装 Test')
if (Test-Path -LiteralPath $target) { throw 'Test target must be new.' }
$settingsFile = Join-Path $env:LOCALAPPDATA 'AirspyPanel\AirspyPanel.settings.xml'
function SettingsHash { if (Test-Path -LiteralPath $settingsFile) { return (Get-FileHash -LiteralPath $settingsFile -Algorithm SHA256).Hash }; return 'absent' }
$before = SettingsHash
function RunSetup {
    $p = Start-Process -FilePath $Installer -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="' + $target + '"'),'/GROUP="AirspyPanel Packaging Test"','/TASKS=') -WindowStyle Hidden -Wait -PassThru
    return $p.ExitCode
}
# Test the exact named mutex used by the application without starting reception.
$created = $false
$mutex = New-Object Threading.Mutex($true,'Local\AirspyPanel',[ref]$created)
try {
    if (-not $created) { throw 'A panel instance is running; do not disturb it.' }
    if ((RunSetup) -eq 0) { throw 'Installer did not reject a running application mutex.' }
    if (Test-Path -LiteralPath (Join-Path $target 'AirspyPanel.exe')) { throw 'Busy install wrote the EXE.' }
} finally { if ($created) { $mutex.ReleaseMutex() }; $mutex.Dispose() }
if ((RunSetup) -ne 0) { throw 'Fresh installation failed.' }
$installed = Join-Path $target 'AirspyPanel.exe'
if (-not (Test-Path -LiteralPath $installed)) { throw 'Missing installed program.' }
if ((SettingsHash) -ne $before) { throw 'Installation changed personal settings.' }
$firstHash = (Get-FileHash -LiteralPath $installed).Hash
if ((RunSetup) -ne 0) { throw 'Reinstallation/upgrade failed.' }
if ((SettingsHash) -ne $before -or (Get-FileHash -LiteralPath $installed).Hash -ne $firstHash) { throw 'Upgrade changed settings or executable unexpectedly.' }
$test = Start-Process -FilePath $installed -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
if ($test.ExitCode -ne 0) { throw 'Installed executable self-test failed.' }
$uninstaller = Join-Path $target 'unins000.exe'
$remove = Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -WindowStyle Hidden -Wait -PassThru
if ($remove.ExitCode -ne 0) { throw 'Uninstall failed.' }
if (Test-Path -LiteralPath $installed) { throw 'Uninstall left the application EXE.' }
if ((SettingsHash) -ne $before) { throw 'Uninstall changed personal settings.' }
Write-Output 'PASS: busy-instance blocking, fresh per-user install in Unicode/spaced path, reinstall, installed executable self-test, uninstall and unchanged personal settings. Same-user test, not a clean OS/account certification.'
