# Contributing

Keep the MSN/Aero design, accessible fixed compact mode, and the separation between receiver data and VRS availability. Do not turn missing aircraft into an offline error. Open an issue with a redacted reproduction before broad behavior changes.

## Build and tests

Windows with .NET Framework 4.8 provides the compiler used by `build.ps1`. No NuGet packages are required. Run:

```powershell
.\build.ps1
Start-Process .\bin\AirspyPanel.exe -ArgumentList '--self-test' -Wait
Start-Process .\bin\AirspyPanel.exe -ArgumentList '--render' -Wait
Start-Process .\bin\AirspyPanel.exe -ArgumentList '--render-languages' -Wait
Get-Content .\bin\self-test.txt, .\bin\layout-test.txt, .\bin\international-test.txt
```

The render commands use preview instances, avoid real receiver processes, and do not save personal settings. They produce offscreen images, not proof of real desktop DPI behavior. Inspect representative images as well as assertions. VS Code tasks provide build, run and self-test entry points. `VERSION` is the single application/installer/package version source.

## Installer tooling and packaging

Install Inno Setup 6.7.3 from its [official release](https://github.com/jrsoftware/issrc/releases/tag/is-6_7_3), verifying the download's Authenticode signature. Use its included English/Japanese/French/German/Russian message files. Add the upstream Chinese Simplified message file to the compiler's `Languages` directory; do not change its attribution. The file used for this release has SHA-256 `e0b0b350e2245f3c5e65586dfe43d574f6e7f06f2261149aba284954b3fc9a8d` and is available from [the upstream repository](https://github.com/jrsoftware/issrc/blob/main/Files/Languages/ChineseSimplified.isl). If upstream changes, review it rather than silently accepting another file.

```powershell
$env:INNO_SETUP_COMPILER = 'path-to-Inno-Setup\ISCC.exe'
.\package.ps1
```

Packaging uses an explicit allowlist, writes a fresh staging folder, refuses to replace existing release output and generates SHA-256 hashes for the EXE and ZIP. Do not package `bin`, settings, logs, original decoder files or migration archives. The installer must not kill a running panel, install dependencies silently, or remove VRS during uninstall. Unsigned releases must be described as unsigned.

Before publishing, run privacy scans on source, reachable history and extracted assets. Do not force-push or rewrite history to hide a discovered secret; stop publication and address the exposure first. Preserve public author credit and legitimate third-party notices.

## Translations

See [architecture and localization](docs/architecture.md). Update all six resource files and guides together. Preserve keys, technical names, URLs and format arguments. Add keys instead of reusing an existing key for a different concept. Request native-speaker review; machine-assisted coverage is not a claim of linguistic certification.
