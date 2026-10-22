# AirspyPanel v3.0.0 — Live Aero, six languages, setup and portable editions

AirspyPanel now provides a complete MSN/Aero-inspired interface, a fixed compact panel, light/dark/system themes, tray operation and six instantly switchable languages: English (US), 简体中文, 日本語, Français, Deutsch and Русский.

## Downloads

- **AirspyPanel-v3.0.0-setup.exe** — current-user installer, optional desktop shortcut and uninstaller.
- **AirspyPanel-v3.0.0-portable.zip** — extract the complete archive and run the EXE; no installation required.
- **SHA256SUMS.txt** — hashes for both downloads.

Both editions require Windows 10/11 and .NET Framework 4.8. They use the same per-user settings directory. These builds are unsigned; keep security protections enabled.

## Changes since v2.0.0

- Full/compact internal navigation, aligned Aero controls, relative average/peak signal meters, aircraft list and airplane icon.
- Separate VRS API, source and configuration checks; zero aircraft stays online, polling does not repeatedly flash a checking indicator, and stale requests cannot overwrite new settings.
- Local program discovery and official dependency download links. Configuring VRS backs up settings and performs necessary normal restarts without a redundant confirmation.
- **Stop all** and **Exit** stop the panel-owned decoder and close local VRS. Tray minimization continues reception. Failed VRS exit is explicit and can be retried or left running when exiting only the panel.
- Key-event logs with severity, repeat counts, filters and export; raw decoder output is separate.
- Six-language UI and complete six-language guides, stable setting identifiers, backend interfaces and documented contribution/build paths.

## Upgrade and dependencies

Exit the old panel before updating. Existing settings, ports and program paths are retained; old Chinese settings remain Chinese. New installations follow the system language with English fallback. Uninstall preserves settings unless explicitly asked to remove them. The portable edition also stores settings under `%LOCALAPPDATA%\AirspyPanel`.

Airspy ADS-B SPY, its DLLs/drivers and VRS are **not bundled**. Obtain them from the official sites linked in the panel and guide. VRS may additionally require Windows .NET Framework 3.5. This project is independent and its MIT license does not relicense third-party software.

## Validation and limitations

Decoder/Beast/VRS fixture tests, 48 existing layout cases, 672 internationalized offscreen renders and real per-user install/reinstall/uninstall checks passed. Privacy checks exclude personal paths/settings, reception logs and backups from the release. Hardware reception, physical multi-monitor DPI changes and real VRS graceful-exit behavior are not claimed as end-to-end validated. Translations welcome native-speaker review.

[English guide](https://github.com/PriscillaOvO/AirspyPanel/blob/main/docs/guide/en-US.md) · [中文](https://github.com/PriscillaOvO/AirspyPanel/blob/main/docs/guide/zh-CN.md) · [日本語](https://github.com/PriscillaOvO/AirspyPanel/blob/main/docs/guide/ja-JP.md) · [Français](https://github.com/PriscillaOvO/AirspyPanel/blob/main/docs/guide/fr-FR.md) · [Deutsch](https://github.com/PriscillaOvO/AirspyPanel/blob/main/docs/guide/de-DE.md) · [Русский](https://github.com/PriscillaOvO/AirspyPanel/blob/main/docs/guide/ru-RU.md)

See the repository README for screenshots and the data-flow diagram, and `docs/verification.md` for precise coverage and limits.
