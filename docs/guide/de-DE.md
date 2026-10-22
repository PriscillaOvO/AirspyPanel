# AirspyPanel 3.0 – Benutzerhandbuch

[English](en-US.md) · [简体中文](zh-CN.md) · [日本語](ja-JP.md) · [Français](fr-FR.md) · [Deutsch](de-DE.md) · [Русский](ru-RU.md)

## Installation und Aktualisierung

AirspyPanel ist ein unabhängiges Community-Programm für Airspy-ADS-B-Empfang und Virtual Radar Server (VRS), kein offizielles Produkt dieser Anbieter. Voraussetzung sind Windows 10/11 und .NET Framework 4.8. Die Entwicklungsprüfungen erfolgen unter Windows 11 x64; automatische Tests zertifizieren weder andere Systeme noch echte Empfangshardware.

Installations-EXE oder portables ZIP von den [Releases](https://github.com/PriscillaOvO/AirspyPanel/releases) laden. SHA-256 mit PowerShell `Get-FileHash` und `SHA256SUMS.txt` vergleichen. Die Pakete sind nicht signiert; Sicherheitssoftware nicht deaktivieren. Installation nur für den aktuellen Benutzer, optionales Desktopsymbol, kein Autostart. Vor Updates das Panel beenden. Einstellungen bleiben bei Updates und standardmäßig bei Deinstallation erhalten; Löschen erfordert eine ausdrückliche Auswahl. Stille Deinstallation behält sie.

ZIP vollständig entpacken, `AirspyPanel.exe` mit der zugehörigen `.config` starten. Beide Ausgaben speichern unter `%LOCALAPPDATA%\AirspyPanel`. Bestehende Ports, Verstärkung und Pfade bleiben erhalten. Alte chinesische Einstellungen behalten Chinesisch; neue folgen der Systemsprache mit Englisch als Rückfall.

## Erste Schritte

[ADS-B SPY für Windows](https://airspy.com/download/) und [VRS](https://www.virtualradarserver.co.uk/Download.aspx) separat installieren. Decoder-DLLs aus dem Originalpaket behalten. Drittprogramme und Treiber sind nicht enthalten. VRS kann zusätzlich das Windows-Feature .NET Framework 3.5 benötigen.

Unter Einstellungen Programme suchen oder unter Programme und Karte `airspy_adsb.exe` und `VirtualRadar.exe` auswählen. Die abbrechbare Suche prüft übliche Orte und überspringt unzugängliche Ordner und Verzeichnislinks. Sie durchsucht nicht alle Laufwerke, startet nichts und ändert keine VRS-Konfiguration. Bei Bedarf einen genaueren Ordner wählen. Gültige Pfade bleiben, mehrere Treffer erfordern eine Auswahl. Ergebnisse ausdrücklich speichern.

Airspy R0/R2/Mini mit 1090-MHz-Antenne verbinden und andere Programme mit Gerätezugriff schließen. Empfang starten, VRS einrichten und starten, dann Karte öffnen. Laufendes lokales VRS wird normal geschlossen, die Konfiguration gesichert und aktualisiert und VRS ohne zweite Bestätigung neu gestartet. Schließfehler werden gemeldet, VRS wird nicht zwangsweise beendet.

## Karte, Status und Pegel

Standardadresse: `http://127.0.0.1/VirtualRadar/`. Auch `/VirtualRadar/desktop.html#` wird akzeptiert; daraus entsteht `AircraftList.json`. Geänderten Webport in der URL eintragen. Decoder und VRS benötigen denselben TCP-Datenport (Standard 30005) und Beast. Der Webport ist davon unabhängig.

Grau: ungeprüft; Grün: API, Quelle und Konfiguration passen; Gelb: API funktioniert, Quelle/Konfiguration prüfen; Rot: nicht erreichbar. Status anklicken für Details und Prüfzeit. Null Flugzeuge ist ein gültiger Onlinezustand. Kartenkacheln und lokale API sind getrennte Fehlerquellen. Mehrere Empfänger lassen sich in Einstellungen auswählen, eigene Namen sind erlaubt. Lokale Konfigurationsabweichungen blockieren das Öffnen der Karte nicht.

Mittel- und Spitzenpegel stammen aus drei Sekunden Beast-Nachrichten, von 0–255 auf 0–100 % abgebildet. Keine kalibrierten dBm, kein Spektrum oder Rauschboden. Flugzeuge werden anhand expliziter DF11/17-ICAO-Adressen innerhalb von 60 Sekunden dedupliziert; VRS kann anders zählen.

## Bedienung und Einstellungen

Das volle Fenster ist skalierbar; die kompakte Ansicht ist etwa 320×620 DIP groß und auf den Monitorarbeitsbereich begrenzt. Einstellungen, Protokoll und Details bleiben im Fenster. Ansichtswechsel startet den Empfang nicht neu. Helles, dunkles und Systemdesign sowie sechs Sprachen stehen bereit. Sprachwechsel wirkt sofort und behält Empfang und Eingaben; fremde Rohausgaben und Systemfehlerdetails bleiben im Original.

Alles stoppen beendet den Panel-Decoder und schließt lokales VRS, auch manuell gestartetes. Browser-Kartentabs bleiben offen. Programm beenden verwendet denselben Ablauf. Bei VRS-Schließfehlern erneut versuchen, abbrechen oder ausdrücklich nur das Panel beenden. Minimieren in den Infobereich empfängt weiter. Die erste Schließauswahl kann gespeichert und unter Darstellung und Schließen geändert werden. Doppelklick auf das Symbol stellt die Seite wieder her; erneuter Programmstart aktiviert die vorhandene Instanz.

Das Protokoll liegt unter Einstellungen. Wichtige Ereignisse werden bevorzugt, Warnungen/Fehler hervorgehoben, Wiederholungen zusammengefasst; Rohausgabe ist getrennt. Hochscrollen pausiert das Folgen, Neueste setzt es fort. Export berücksichtigt Ansicht und Filter. Pro Ansicht bleiben höchstens 500 Einträge im Speicher; normale Empfangsprotokolle werden nicht automatisch gespeichert.

Erweiterte Parameter: Verstärkung auto oder 0–21; zu hoch kann verzerren. 12/20/24 MHz bezeichnet Abtastung, nicht 1090-MHz-Empfang und kein eigenständiges MLAT; 24 MHz nur mit passendem R2. Filter 1–60, Standard 4; höhere Werte können mehr CPU benötigen. USB-Packing `-p` betrifft die Übertragung, nicht Bias-Tee-Strom. Empfohlene Werte überschreiben weder Pfade noch Karten-URL.

## Fehlerbehebung, Entwicklung und Lizenz

Ohne Nachrichten Gerätbelegung, Antenne, USB, Treiber und Empfangslage prüfen. Bei VRS Adresse, Webport und Dienst prüfen. Details unterscheiden Timeout, falsche HTML/JSON-Antwort und Quellenabweichung. Bei AdGuard-Seiten lokale Filterregeln prüfen. Null Flugzeuge allein ist kein Fehlernachweis.

VRS-Sicherungen liegen bei der Konfiguration unter `%LOCALAPPDATA%\VirtualRadar`. VRS vor manueller Wiederherstellung beenden. Fehlerberichte mit Version, Windows, Sprache und Schritten senden; private Pfade, Standort, Empfangsdaten und Zugangsdaten vorher entfernen. Tatsächlich geprüfte Bereiche stehen im Prüfbericht.

Build: `powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1`, Ausgabe in `bin`. Tests: `bin\AirspyPanel.exe --self-test`, `--render` und `--render-languages`. Installer-Voraussetzungen siehe CONTRIBUTING.md.

Datenfluss: Airspy → ADS-B SPY → Beast TCP → Panel und VRS → Browserkarte. Das Panel steuert und überwacht, ersetzt aber weder Decoder noch Kartenserver. Eigener Code und Dokumentation: MIT. Drittprodukte behalten ihre Lizenzen; ein Weiterverteilungsrecht für ADS-B SPY wird nicht vorausgesetzt.
