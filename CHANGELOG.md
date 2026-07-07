# Changelog

Alle nennenswerten Änderungen an diesem Projekt. Format nach
[Keep a Changelog](https://keepachangelog.com/de/1.1.0/), Versionierung nach
[SemVer](https://semver.org/lang/de/).

## [Unreleased]

## [0.1.7] – 2026-07-07

### Behoben
- **Plugin lud nicht in XrmToolBox** (erschien nicht in der Tool-Liste): Die gemergte
  Assembly referenzierte extern `Microsoft.Bcl.HashCode 1.0.0.0`, das XrmToolBox weder
  ausliefert noch per Binding-Redirect abdeckt → `FileNotFoundException` beim Laden.
  `Microsoft.Bcl.HashCode` wird jetzt **mit in `LookupImportPlus.dll` gemergt**. Die
  übrigen `System.*`-Abhängigkeiten deckt XrmToolBox per Binding-Redirect ab und bleiben
  extern. Verifiziert: Plugin lädt gegen den echten XrmToolBox-Assembly-Satz.

## [0.1.6] – 2026-07-07

### Behoben
- **Store-Validierung „assembly version must match package version" (endgültig):**
  Die Tool Library verlangt, dass **jede** Assembly im Paket die Paketversion trägt.
  Third-Party-Bibliotheken (ClosedXML, ClosedXML.Parser, DocumentFormat.OpenXml,
  ExcelNumberFormat, SixLabors.Fonts, RBush) lassen sich nicht umversionieren, daher
  werden sie jetzt via **ILRepack in `LookupImportPlus.dll` hineingemergt** — das Paket
  enthält nur noch **eine** Assembly mit der Paketversion (wie die kanonischen Plugins,
  z. B. PluginTraceViewer). Plugin-Load und der ClosedXML-Excel-Pfad sind mit der
  gemergten Assembly verifiziert.

## [0.1.5] – 2026-07-07

### Behoben
- **Store-Validierung „Logo Url is not valid":** Das eingebettete `<icon>` wurde
  entfernt; das Paket nutzt jetzt nur noch `<iconUrl>` — exakt wie die kanonischen
  MscrmTools-/Cinteros-Plugins. Das eingebettete Icon ließ nuget die Icon-Behandlung
  überschreiben, was die Tool Library ablehnte.

## [0.1.4] – 2026-07-07

### Behoben
- **Store-Validierung „assembly version must match package version":** Die net48-
  Framework-Facades (`System.Memory`, `System.Buffers`, `System.ValueTuple`,
  `System.Runtime.CompilerServices.Unsafe`, `System.Numerics.Vectors`,
  `System.Threading.Tasks.Extensions`) und `Microsoft.Bcl.HashCode` werden **nicht
  mehr mitgeliefert**. Sie sind bereits in XrmToolBox / im .NET Framework enthalten
  und lösen auf net48 auf die Framework-Version (z. B. `4.8.4161.0`) auf, was die
  Store-Prüfung scheitern ließ. Das Paket enthält jetzt nur noch das Plugin plus die
  echten Fremdbibliotheken (ClosedXML-Closure), wie es die XrmToolBox-Doku verlangt.

## [0.1.3] – 2026-07-07

### Geändert
- XrmToolBox-Dependency auf die kanonische Host-Version `1.2025.10.74` gesetzt
  (identisch zu `XrmToolBoxPackage` und den offiziellen MscrmTools-Plugins). Zusammen
  mit dem Unlisten der dependency-losen 0.1.0 sind damit alle gelisteten Versionen
  Store-konform.

## [0.1.2] – 2026-07-07

### Geändert
- XrmToolBox-Dependency-Untergrenze auf einen breit kompatiblen net48-Wert
  (`1.2024.1.1`) gesetzt, damit das Tool auf jedem aktuellen XrmToolBox angeboten
  wird. Erste vollständig Store-konforme Version, die keylos via Trusted
  Publishing veröffentlicht wird.

## [0.1.1] – 2026-07-07

### Hinzugefügt
- **MIT-Lizenz** und eine auf der nuget.org-Paketseite angezeigte **Readme**
  (behebt die „License/Readme missing"-Hinweise von 0.1.0).

### Geändert
- Paketierung warnungsfrei (`NU5100` bewusst unterdrückt; DLLs liegen konstruktions-
  bedingt unter `Plugins/`). Erste Veröffentlichung über **Trusted Publishing**.

## [0.1.0] – 2026-07-06

Erste Version: vollständiger Port der Power-Apps-Code-App
[brunsforge/LookupImportPlus](https://github.com/brunsforge/LookupImportPlus) auf ein
XrmToolBox-Plugin (WinForms, .NET Framework 4.8) über das Dataverse-**SDK**.

### Hinzugefügt
- **Domänenmodell** 1:1 aus `src/domain/*` (Config, Conditions, Metadata, Import, Issues, Template).
- **Deterministische Lookup-Auflösung** (`LookupResolver`): feste Reihenfolge
  GUID → Business Key → Suchfeld; nie geraten, Mehrdeutigkeit wird eskaliert.
- **Bedingungs-Compiler**: `ConditionGroup` → `QueryExpression` + Zeitanker (relatives Datum)
  + menschenlesbarer Audit-Filter.
- **Import-Runner**: Dry Run (mit Dedup-Cache), Konfliktentscheidung, Commit via
  `ExecuteMultipleRequest` (Batch, ContinueOnError).
- **Schema-Drift-Preflight** (`ConfigValidationService`) + Metadaten-Fingerprint.
- **Excel-Round-Trip** (ClosedXML): Template/Datenexport mit verstecktem `_LookupImportPlus`-
  Manifest (djb2-Hash) und technischen Lookup-Spalten; Reimport mit Manifest-Prüfung.
- **UI**: Shell + sechs Screens (Job-Konfigurationen, Editor mit 4-Tab-Assistent inkl.
  pro-Ziel-Lookupkonfiguration + Bedingungseditor, Importlauf, Konfliktkorb, Konflikt
  auflösen, Historie), Daten-Vorschau-Modal, Statuskacheln/-farben, DE/EN.
- **Persistenz** von Konfigurationen und Historie als JSON im XrmToolBox-Settings-Ordner.
- **Deployment**: `build/package.ps1` (Drop-in-Ordner + Zip), `build/release.ps1`
  (Versionsbump + Pack) und Store-`.nupkg` via `build/pack`.

### Abweichungen von der Quelle (bewusst)
- **Leere Lookups nicht blockierend**: Eine komplett leere Lookup-Spalte ergibt den
  nicht-blockierenden Status `Empty` (Zeile bleibt schreibbar, Lookup ungesetzt) statt
  des blockierenden `NotFound` der Vorlage. Gefüllt-aber-nicht-gefunden bleibt `NotFound`.

### Verifiziert
- Plugin lädt in XrmToolBox (MEF-Metadaten, Control-Instanziierung).
- 21 Kernlogik-Tests + 15 End-to-End-Tests gegen eine echte Dataverse-Org grün.
- Release-Build: 0 Warnungen, 0 Fehler.

[Unreleased]: https://github.com/brunsforge/XMRToolbox_LookupImportPlus/compare/v0.1.7...HEAD
[0.1.7]: https://github.com/brunsforge/XMRToolbox_LookupImportPlus/compare/v0.1.6...v0.1.7
[0.1.6]: https://github.com/brunsforge/XMRToolbox_LookupImportPlus/compare/v0.1.5...v0.1.6
[0.1.5]: https://github.com/brunsforge/XMRToolbox_LookupImportPlus/compare/v0.1.4...v0.1.5
[0.1.4]: https://github.com/brunsforge/XMRToolbox_LookupImportPlus/compare/v0.1.3...v0.1.4
[0.1.3]: https://github.com/brunsforge/XMRToolbox_LookupImportPlus/compare/v0.1.2...v0.1.3
[0.1.2]: https://github.com/brunsforge/XMRToolbox_LookupImportPlus/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/brunsforge/XMRToolbox_LookupImportPlus/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/brunsforge/XMRToolbox_LookupImportPlus/releases/tag/v0.1.0
