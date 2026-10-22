# Third-party notices

AirspyPanel is an independent community project. Airspy, ADS-B SPY and Virtual Radar Server names identify interoperable products; no affiliation or endorsement is implied.

The panel's code, documentation and airplane artwork are distributed under the repository's MIT license. That license does not grant rights to third-party products.

- **ADS-B SPY, Airspy drivers and decoder DLLs:** not included. Obtain them from https://airspy.com/download/ and follow the applicable terms. A public download link is not treated as permission to redistribute.
- **Virtual Radar Server:** not included. Its published license permits redistribution subject to preservation of copyright, conditions and disclaimer, and prohibits using contributor names for endorsement without permission. Any future bundled distribution must also audit included dependencies. See https://www.virtualradarserver.co.uk/License.aspx and https://www.virtualradarserver.co.uk/Download.aspx.
- **Microsoft .NET Framework:** system prerequisite, not bundled. https://dotnet.microsoft.com/download/dotnet-framework/net48
- **Inno Setup:** installer tooling/runtime by Jordan Russell and Martijn Laan. Used under the Inno Setup license; it does not change the panel's MIT license. https://github.com/jrsoftware/issrc/blob/main/license.txt . Installer translations retain the credits of their upstream maintainers; Chinese translation by Zhenghan Yang (Kira), Japanese translation maintained by Koichi Shirasuka, and other contributors as listed in the compiler language files.

No telemetry or automatic software-update service is added. The panel connects to the configured VRS URL and the local decoder data port; map pages may load online map resources through the browser.
