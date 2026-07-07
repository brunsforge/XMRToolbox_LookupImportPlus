# Entwicklung

## Voraussetzungen

- **.NET SDK** (baut das SDK-style-Projekt) und **.NET Framework 4.8 Targeting Pack**
  (bzw. Visual Studio mit „.NET-Desktopentwicklung"). Die net48-Referenzassemblies
  liegen unter `…\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.X`.
- Zum Testen: eine Dataverse-Umgebung (die E2E-Tests nutzen eine App-Registrierung mit
  Client-Secret).

## Bauen

```bash
dotnet restore
dotnet build src/LookupImportPlus/LookupImportPlus.csproj -c Release
```

Ergebnis: `src/LookupImportPlus/bin/Release/LookupImportPlus.dll`.

## Warum net48 (nicht 4.6.2)

Der Handoff nannte 4.6.2 als historische Untergrenze; der aktuelle Host verlangt **net48**:
`XrmToolBoxPackage` 1.2025.10.74 und `MscrmTools.Xrm.Connection` liefern nur net48-Assets.
Außerdem ist `MscrmTools.Xrm.Connection` explizit auf **1.2025.9.64** gepinnt – ohne den
Pin löst die transitive Abhängigkeit auf 1.2025.7.63 auf, während die gebündelte
`XrmToolBox.Extensibility` gegen .9.64 kompiliert wurde (Buildfehler CS1705). Beides in
`LookupImportPlus.csproj` dokumentiert.

## Projektstruktur

Ein Assembly, nach Ordnern gegliedert. Übersicht siehe [ARCHITECTURE.md](ARCHITECTURE.md)
und die Struktur im README. Abhängigkeiten zeigen nur nach unten
(Domain → Data → Services → App → UI).

## Tests

Es gibt (bewusst) kein eingechecktes Testprojekt; verifiziert wird über zwei
Wegwerf-Harnische, die gegen das gebaute Assembly laufen:

- **Kernlogik (ohne Verbindung):** Excel-Round-Trip, Manifest-Hash (+ Tamper),
  Condition-Compiler, Template-Spalten, JSON-Persistenz, Status-Klassifikation.
- **End-to-End (mit Verbindung):** Metadaten inkl. polymorpher Lookup-Targets,
  Resolver in allen Stufen, Dry Run, Commit via `ExecuteMultipleRequest`, real
  geschriebene `EntityReference`-Bindung; Testdaten werden angelegt und wieder gelöscht.
  Zugangsdaten kommen aus Umgebungsvariablen (`DV_URL`, `DV_APPID`, `DV_SECRET`) – nie
  aus Dateien.

Ein Plugin-Load-Smoke-Test lädt das Assembly per Reflection, prüft die MEF-Metadaten und
instanziiert das WinForms-Control.

## Release

Eine einzige Versionsquelle: `Directory.Build.props` (`<Version>`). Sie wird sowohl von
der Plugin-DLL (Assembly-Version → XrmToolBox-Update-Erkennung) als auch vom Store-Paket
geerbt.

```powershell
build\release.ps1 0.1.1     # Version setzen · bauen · deploy\Plugins + Zip + .nupkg
```

- `build/package.ps1` – bündelt nur die Nicht-Host-Assemblies (Plugin + ClosedXML-Closure
  + net48-Polyfills) nach `deploy/Plugins` (+ Zip).
- `build/pack/LookupImportPlus.Pack.csproj` – `dotnet pack` → Store-`.nupkg`.

### CI/CD (GitHub Actions)

- `.github/workflows/ci.yml` – baut bei Push/PR auf `windows-latest` und lädt das
  gebündelte Plugin als Artefakt hoch.
- `.github/workflows/release.yml` – veröffentlicht bei einem Tag `vX.Y.Z` nach nuget.org
  per **Trusted Publishing** (OIDC, kein gespeicherter Key). Einrichtung und der
  alternative API-Key-Weg: [PUBLISHING.md](PUBLISHING.md).

Der übliche Release ist damit: `build\release.ps1 X.Y.Z` committen, dann `git tag vX.Y.Z`
und den Tag pushen – der Workflow erledigt Build, Pack und Push.
