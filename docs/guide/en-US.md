# AirspyPanel 3.0 user guide

[English](en-US.md) · [简体中文](zh-CN.md) · [日本語](ja-JP.md) · [Français](fr-FR.md) · [Deutsch](de-DE.md) · [Русский](ru-RU.md)

## Install and upgrade

AirspyPanel is a community Windows control panel for Airspy ADS-B reception and Virtual Radar Server (VRS). It is not an Airspy or VRS product. Windows 10/11 and .NET Framework 4.8 are required; Windows 11 x64 is the development test platform. Other systems and actual receiver hardware are not certified by the automated tests.

Download the setup EXE or portable ZIP from [Releases](https://github.com/PriscillaOvO/AirspyPanel/releases). Verify its SHA-256 against `SHA256SUMS.txt` with PowerShell `Get-FileHash`. The release is unsigned; do not disable security software. The installer installs for the current user, offers an optional desktop shortcut, and does not enable startup at login. Exit the panel before upgrading. Settings survive upgrades; uninstall keeps them unless you explicitly choose to remove them. Silent uninstall keeps settings.

For the portable edition, extract the entire ZIP and run `AirspyPanel.exe`, keeping its `.config` file beside it. Both editions store settings in `%LOCALAPPDATA%\AirspyPanel`, not beside the EXE. Existing settings are imported without resetting gain, port or paths. Old Chinese settings remain Chinese; new installs follow the system language, falling back to English.

## First use

Install [ADS-B SPY for Windows](https://airspy.com/download/) and [VRS](https://www.virtualradarserver.co.uk/Download.aspx) separately. Keep the decoder's original DLLs. Neither third-party software nor drivers are bundled. VRS may separately require the Windows .NET Framework 3.5 feature.

Open Settings and use **Find local programs**, or browse to `airspy_adsb.exe` and `VirtualRadar.exe` under Programs and map. The scan checks common locations, can be canceled and skips inaccessible directories and directory links. It does not search every drive, launch dependencies or change VRS configuration. Choose a folder for a narrower search when necessary. Valid existing paths are preserved; ambiguous results require a choice. Save the results explicitly.

Connect an Airspy R0, R2 or Mini and a 1090 MHz antenna. Close other applications using the device. Click **Start reception**, then **Configure & start VRS**, then open the aircraft map. Configuration normally closes running local VRS, backs up its configuration and restarts it without a second confirmation. Failure to close VRS is reported; it is not forcibly killed.

## Map, status and signal levels

The default map is `http://127.0.0.1/VirtualRadar/`. A desktop URL such as `/VirtualRadar/desktop.html#` is also accepted. Use the correct web port if changed. The panel derives `AircraftList.json` from the map URL. VRS and the decoder must use the same TCP data port (default 30005) and Beast format. The web port is a separate setting.

Gray means not checked, green means API/source/configuration match, yellow means the API works but source/configuration needs review, and red means unavailable. Click the status for details and the last check time. Zero aircraft is a valid online result. A map-tile problem is distinct from a local API failure. Multiple matching receivers can be selected under Programs and map; custom receiver names are supported. Opening the map is not blocked by local configuration mismatch.

The average and peak meters use relative levels from Beast messages over the last three seconds, mapping 0–255 to 0–100%. They are not calibrated dBm, spectrum or noise-floor measurements. Aircraft are unique explicit ICAO addresses from DF11/17 received within 60 seconds. VRS may use different counting and expiry rules.

## Controls and preferences

The full window is resizable. Compact mode is fixed at approximately 320×620 DIP, limited by the monitor work area. Settings, logs and details open inside it. Switching modes does not restart reception. Light, dark and system themes are supported. Choose one of six languages in Settings; changes apply immediately without resetting reception or inputs. Third-party raw output and system exception details are not translated.

**Stop all** stops the decoder started by the panel and normally closes local VRS, including manually started VRS. Browser tabs remain open. Exiting uses the same stop sequence. If VRS cannot exit, choose retry, cancel, or explicitly exit only the panel. Closing to the tray keeps reception active. The first close asks and can remember your choice; change it in Appearance and closing. Double-click the tray icon to restore the same page. A second launch restores the existing instance.

Logs are under Settings → Event log. Key events are the default; warnings/errors are emphasized, repeated events are merged, and raw decoder output is separate. Scrolling up pauses following; Latest resumes it. Export follows the selected view and level. Each view keeps up to 500 entries in memory; ordinary reception logs are not automatically saved.

Advanced settings: gain `auto` or 0–21; excessive gain can distort strong signals. Sampling 12/20/24 MHz is not the 1090 MHz receive frequency and does not provide standalone MLAT; 24 MHz requires a compatible R2. Filter range is 1–60, default 4; higher values can increase CPU use. USB packing (`-p`) is a transport option, not Bias-Tee power. Restore recommended settings does not overwrite paths or map URL.

## Troubleshooting and maintenance

If no messages arrive, check device ownership, antenna, driver, USB and reception conditions. If VRS is offline, check its web address, port and service. Use status details for timeouts, wrong HTML/JSON responses or a mismatched source. Check local web-filtering rules when an AdGuard page is returned. Never interpret zero aircraft alone as a connection error.

VRS configuration backups are alongside its configuration in `%LOCALAPPDATA%\VirtualRadar`; exit VRS before manually restoring one. Report bugs with the version, Windows version, language and reproduction steps; redact local paths, location, receiver data and credentials before sharing logs. See the repository verification record for actual test coverage.

## Build, data flow and license

Run `powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1` from a source checkout. Run `bin\AirspyPanel.exe --self-test`, `--render`, or `--render-languages` for the respective tests. Compiler output is in `bin`; installer prerequisites are documented in CONTRIBUTING.md.

Data flows from Airspy hardware to ADS-B SPY, then over Beast TCP to the panel and VRS; the browser obtains its map from VRS. The panel controls processes and monitors decoded data; it does not replace the radio decoder or the VRS map server. Self-authored code and documentation use MIT. Airspy, VRS and their components keep their own licenses. No redistributable rights for ADS-B SPY are assumed.
