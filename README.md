# LookupImportPlus — XrmToolBox Plugin

*Languages: **English** (below) · [Deutsch](#deutsch)*

Auditable Excel import into Dataverse as an **XrmToolBox plugin** (WinForms, .NET Framework 4.8).
Lookups are **resolved deterministically or escalated to a human — never guessed**.

A port of the Power Apps Code App
[brunsforge/LookupImportPlus](https://github.com/brunsforge/LookupImportPlus) to the
Dataverse **SDK** (instead of the Web API). The authoritative logic specification remains
that source (`src/domain/*`, `src/services/*`).

## Documentation

- [docs/USAGE.md](docs/USAGE.md) — usage, screens, typical flow
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — layers & transport mapping Code App → SDK
- [docs/PUBLISHING.md](docs/PUBLISHING.md) — publishing & updating (nuget.org / Tool Store)
- [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) — build, tests, release
- [CHANGELOG.md](CHANGELOG.md) — changes per version

*(The docs under `docs/` are currently in German; the UI itself is bilingual EN/DE.)*

## Core rule

Fixed matching order, first hit wins:

```
1) GUID column  →  2) Business Key  →  3) Search field + conditions
```

0 hits ⇒ Not found · exactly 1 ⇒ Resolved · several ⇒ Ambiguous → conflict strategy
(`escalate` / `skip` / `fail`).

## Project structure

```
LookupImportPlus.sln
src/LookupImportPlus/
  LookupImportPlus.csproj        SDK-style, net48, WinForms
  Plugin.cs                      IXrmToolBoxPlugin factory (MEF export + metadata)
  LookupImportPlusControl.cs     PluginControlBase shell (navigation + content)
  Data/
    DataverseContext.cs          SDK seam over IOrganizationService (replaces DataverseClient)
  Domain/                        Source-of-truth types (Config, Conditions, Metadata,
                                 Import, Issues, Template, Enums) – 1:1 from src/domain/*
  Services/
    MetadataService.cs           RetrieveEntity/AllEntities → normalized, lookup targets
    LookupResolver.cs            Fixed order GUID → Business Key → Search field (core)
    ConditionCompiler.cs         ConditionGroup → ConditionExpression + time anchors + audit
    ImportRunner.cs              Dry run · conflict decision · commit (ExecuteMultiple)
    ConfigValidationService.cs   Schema-drift preflight + fingerprint
    DataExportService.cs         Query + CRM/schema rows (incl. business-key enrichment)
    ConfigBuilder.cs             Build config from metadata (editor)
    ViewService.cs               Saved views (savedqueries) + FetchXML columns
    PersistedStore.cs            JSON persistence (configs/history) instead of localStorage
    Json.cs                      Deterministic JSON settings (manifest hash/persistence)
    Excel/                       TemplateColumns · ManifestHash (djb2) ·
                                 ExcelTemplateService/ExcelParserService (ClosedXML)
  App/
    I18n.cs                      EN/DE strings (port of src/i18n.ts)
    AppContainer.cs              Composition root + configs/history/active run
  UI/
    IScreenHost.cs · ScreenControlBase.cs · UiTheme.cs · ConflictKey.cs
    DataPreviewModal.cs          3.3 data preview (CRM/schema columns)
    Screens/                     Configs · Editor (4 tabs) · ImportRun · Conflicts ·
                                 Resolve · History
```

## Screens (1:1 with the Code App)

`Configs` (home) · `Editor` (wizard, 4 tabs) · `ImportRun` · `Conflicts` · `Resolve` ·
`History`. See `handoff/xrmtoolbox-plugin-handoff.md` for the full screen-by-screen spec.

## Build

Prerequisites: .NET SDK + .NET Framework 4.8 targeting pack (or Visual Studio with the
.NET desktop workload). The current XrmToolBox host requires net48.

```powershell
dotnet restore
dotnet build -c Release
```

Details, tests and the net48 / version-pin background: [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Release & deployment

One command sets the version (single source: `Directory.Build.props`), builds and produces
the drop-in folder **and** the store package:

```powershell
build\release.ps1 0.1.1
# → deploy\Plugins\           (copy into %AppData%\MscrmTools\XrmToolBox\Plugins)
# → deploy\LookupImportPlus-0.1.1.0.zip
# → deploy\LookupImportPlus.0.1.1.nupkg   (tag "XrmToolBox Plugin")
```

Publishing goes to nuget.org (the store scans it). Recommended via **Trusted Publishing**
from GitHub Actions (no stored key) — a `vX.Y.Z` tag starts the release workflow. Setup +
API-key alternative: [docs/PUBLISHING.md](docs/PUBLISHING.md).

**Manual install for testing:** copy the contents of `deploy\Plugins\` into
`%AppData%\MscrmTools\XrmToolBox\Plugins` and restart XrmToolBox.

## Implementation status

Fully ported and runnable:

- **Domain** 1:1 from `src/domain/*` (Config, Conditions, Metadata, Import, Issues, Template).
- **Services** ported: MetadataService, LookupResolver (fixed order, never guesses),
  ConditionCompiler (→ QueryExpression + time anchors), ImportRunner (dry run, conflict
  decision, commit via `ExecuteMultipleRequest`), ConfigValidationService (schema drift),
  DataExportService, ExcelTemplate/Parser (ClosedXML), ViewService, PersistedStore.
- **UI** all six screens + data-preview modal, status tiles/colors, EN/DE with a manual switch.
- **Verified:** the plugin loads in XrmToolBox (MEF metadata, control instantiation);
  core-logic tests green (Excel round-trip, manifest hash + tamper, condition compiler,
  JSON persistence, status classification). Release build: 0 warnings, 0 errors.
- **Verified end-to-end against a real Dataverse org** (ClientSecret): metadata incl.
  polymorphic lookup targets + navigation-property enrichment, resolver in all stages
  (GUID / search field / NotFound / Ambiguous), dry run, commit via `ExecuteMultipleRequest`,
  and a real written + read-back `EntityReference` binding.

Empty-lookup behavior (intentional deviation from the source): a **completely empty** lookup
column (no GUID/business key/search value) is treated as the non-blocking status `Empty` — the
row stays writable, the lookup unset. A *filled but unresolvable* value stays `NotFound`
(blocking).

## License

[MIT](LICENSE) © 2026 brunsforge.

---

<a id="deutsch"></a>

## Deutsch

*Sprachen: [English](#lookupimportplus--xrmtoolbox-plugin) · **Deutsch***

Auditierbarer Excel-Import in Dataverse als **XrmToolBox-Plugin** (WinForms, .NET Framework 4.8).
Lookups werden **deterministisch aufgelöst oder an einen Menschen eskaliert — nie geraten**.

Portierung der Power-Apps-Code-App
[brunsforge/LookupImportPlus](https://github.com/brunsforge/LookupImportPlus) auf das
Dataverse-**SDK** (statt Web API). Maßgebliche Logik-Spezifikation bleibt der dortige
Quellcode (`src/domain/*`, `src/services/*`).

### Dokumentation

- [docs/USAGE.md](docs/USAGE.md) — Bedienung, Screens, typischer Ablauf
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — Schichten & Transport-Mapping Code App → SDK
- [docs/PUBLISHING.md](docs/PUBLISHING.md) — Veröffentlichen & Aktualisieren (nuget.org / Tool Store)
- [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) — Build, Tests, Release
- [CHANGELOG.md](CHANGELOG.md) — Änderungen je Version

### Kernregel

Feste Matching-Reihenfolge, erster Treffer gewinnt:

```
1) GUID-Spalte  →  2) Business Key  →  3) Suchfeld + Bedingungen
```

0 Treffer ⇒ NotFound · genau 1 ⇒ aufgelöst · mehrere ⇒ Mehrdeutig → Konfliktstrategie
(`escalate` / `skip` / `fail`).

### Screens (1:1 zur Code App)

`Configs` (Startseite) · `Editor` (Assistent, 4 Tabs) · `ImportRun` · `Conflicts` ·
`Resolve` · `History`. Siehe `handoff/xrmtoolbox-plugin-handoff.md` für die vollständige
Screen-für-Screen-Spezifikation.

### Build

Voraussetzungen: .NET SDK + .NET Framework 4.8 Targeting Pack (bzw. Visual Studio
mit .NET-Desktop-Workload). Der aktuelle XrmToolBox-Host verlangt net48.

```powershell
dotnet restore
dotnet build -c Release
```

Details, Tests und die net48-/Version-Pin-Hintergründe: [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

### Release & Deployment

Ein Befehl setzt die Version (eine Quelle: `Directory.Build.props`), baut und erzeugt
den Drop-in-Ordner **und** das Store-Paket:

```powershell
build\release.ps1 0.1.1
# → deploy\Plugins\           (in %AppData%\MscrmTools\XrmToolBox\Plugins kopieren)
# → deploy\LookupImportPlus-0.1.1.0.zip
# → deploy\LookupImportPlus.0.1.1.nupkg   (Tag "XrmToolBox Plugin")
```

Veröffentlicht wird nach nuget.org (der Store scannt das). Empfohlen per **Trusted
Publishing** aus GitHub Actions (ohne gespeicherten Key) — ein Tag `vX.Y.Z` startet den
Release-Workflow. Einrichtung + API-Key-Alternative: [docs/PUBLISHING.md](docs/PUBLISHING.md).

**Manuelle Installation zum Testen:** den Inhalt von `deploy\Plugins\` nach
`%AppData%\MscrmTools\XrmToolBox\Plugins` kopieren und XrmToolBox neu starten.

### Verhalten leerer Lookups

Bewusste Abweichung von der Quelle: Eine **komplett leere** Lookup-Spalte (kein GUID/Business
Key/Suchwert) wird als nicht-blockierender Status `Empty` geführt — die Zeile bleibt schreibbar,
der Lookup ungesetzt. Ein *gefüllter, aber nicht auffindbarer* Wert bleibt weiterhin `NotFound`
(blockierend).

### Lizenz

[MIT](LICENSE) © 2026 brunsforge.
