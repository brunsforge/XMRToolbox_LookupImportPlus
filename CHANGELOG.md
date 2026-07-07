# Changelog

Alle nennenswerten Änderungen an diesem Projekt. Format nach
[Keep a Changelog](https://keepachangelog.com/de/1.1.0/), Versionierung nach
[SemVer](https://semver.org/lang/de/).

## [Unreleased]

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

[Unreleased]: https://github.com/brunsforge/XMRToolbox_LookupImportPlus/compare/v0.1.1...HEAD
[0.1.1]: https://github.com/brunsforge/XMRToolbox_LookupImportPlus/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/brunsforge/XMRToolbox_LookupImportPlus/releases/tag/v0.1.0
