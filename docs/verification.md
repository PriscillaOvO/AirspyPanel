# v3.0.0 verification record

Date: 2026-10-22. Development platform: Windows 11 x64, .NET Framework 4.8 WPF. Results below distinguish automated simulation, offscreen rendering and actual installation from unperformed hardware/desktop checks.

## Passed

- Clean-directory compilation and application version 3.0.0.0, with a single VERSION source for the application and installer.
- Existing self-tests: argument validation, settings roundtrip, owned child process output/stop/restart, split/escaped Beast framing, aircraft deduplication/callsign/altitude, signal statistics/expiry, loopback TCP reconnect, VRS configuration backup/preservation/idempotence and HTTP source selection.
- VRS fixtures: zero aircraft, missing/wrong source, custom receiver names, multiple candidates, unreadable configuration, invalid HTML/JSON, HTTP errors, timeout and offline. No synthetic data was sent to a real VRS instance.
- Discovery fixtures: unique/multiple/missing candidates, preservation of valid paths, cancellation and scan budgets.
- Settings migration: old XML retains Chinese, legacy sampling migrates to `auto`, language persists, unsupported system language falls back to English. Personal settings are not written by preview tests.
- 48 existing layout combinations plus 672 combinations of six languages × two themes × two modes × seven pages × four DPI values (96/144/192/240), rendered offscreen. Representative English, Russian, French and German images were visually reviewed; the entire matrix is not a manual desktop inspection.
- Fixed compact mode, VRS control order/equal dimensions, settings/log roundtrip, shared state, non-flashing checks, stale result rejection and full-window size/maximized restoration. New language tests preserve input/statistics, re-render existing events, keep opaque path parameters intact and validate resource-key parity.
- Simulated stop-all: pending operations are guarded against repeat clicks; a failed VRS close is not reported as success and operation controls recover. Existing close-choice cancellation/remembering, named-event wake, log filtering/merging/bounds/export and scroll-follow tests continue to pass.
- Real per-user installer execution in a new temporary directory with spaces and non-ASCII characters, reinstall, installed EXE self-test and silent uninstall. The exact application mutex blocks installation while running. Personal settings hashes are identical before and after installation, reinstall and removal. This is a new directory under the existing user, not a clean Windows account or VM.
- Source/reachable-history privacy-pattern scan and manual review of public author identity, package allowlist and preview screenshots. Backups are outside the repository; packaged settings, reception logs and third-party executables are excluded.

## Explicit limitations

- No fresh Windows VM/account, Windows 10 machine, ARM/x86 hardware or missing-.NET environment was available. Dependency detection is implemented, but installing .NET on a missing-runtime machine was not exercised.
- Rendering at four DPI values is not equivalent to dragging a live window between physical monitors. Real keyboard-focus traversal, continuous resizing, tray interactions, Windows shutdown, Explorer restart and multi-monitor restore need desktop validation.
- Real Airspy sustained reception, device removal/reconnect and the current VRS installation's graceful exit prompts have not been validated end to end in this release run. Radio reception was not interrupted for testing.
- Exit retry/cancel/leave-VRS choices are implemented; actual VRS failure dialogs and system shutdown were not forced on the user's desktop.
- Six-language coverage and resource consistency are checked, but translations have not received native-speaker certification.
- Installer and application are unsigned. No claim of SmartScreen reputation or signing is made.

## Reproduce

See CONTRIBUTING.md for build and test commands. `scripts/test-installer.ps1 -Installer <setup.exe>` refuses to run over an existing installed edition, uses a disposable target and preserves existing settings. `scripts/privacy-check.ps1 -History` reports filenames only when a configured pattern matches; it is a targeted check, not proof that every conceivable secret is absent.

Generated reports and preview images remain local build outputs. Published screenshots are preview states labeled as demonstrations, without live aircraft or private paths.
