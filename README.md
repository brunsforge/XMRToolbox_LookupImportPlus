# LookupImportPlus — XrmToolBox Plugin

Auditierbarer Excel-Import in Dataverse als **XrmToolBox-Plugin** (WinForms, .NET Framework 4.8).
Lookups werden **deterministisch aufgelöst oder an einen Menschen eskaliert — nie geraten**.

Portierung der Power-Apps-Code-App
[brunsforge/LookupImportPlus](https://github.com/brunsforge/LookupImportPlus) auf das
Dataverse-**SDK** (statt Web API). Maßgebliche Logik-Spezifikation bleibt der dortige
Quellcode (`src/domain/*`, `src/services/*`).

## Dokumentation

- [docs/USAGE.md](docs/USAGE.md) — Bedienung, Screens, typischer Ablauf
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — Schichten & Transport-Mapping Code App → SDK
- [docs/PUBLISHING.md](docs/PUBLISHING.md) — Veröffentlichen & Aktualisieren (nuget.org / Tool Store)
- [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) — Build, Tests, Release
- [CHANGELOG.md](CHANGELOG.md) — Änderungen je Version

## Kernregel

Feste Matching-Reihenfolge, erster Treffer gewinnt:

```
1) GUID-Spalte  →  2) Business Key  →  3) Suchfeld + Bedingungen
```

0 Treffer ⇒ NotFound · genau 1 ⇒ aufgelöst · mehrere ⇒ Mehrdeutig → Konfliktstrategie
(`escalate` / `skip` / `fail`).

## Projektstruktur

```
LookupImportPlus.sln
src/LookupImportPlus/
  LookupImportPlus.csproj        SDK-style, net48, WinForms
  Plugin.cs                      IXrmToolBoxPlugin-Factory (MEF-Export + Metadaten)
  LookupImportPlusControl.cs     PluginControlBase-Shell (Navigation + Content)
  Data/
    DataverseContext.cs          SDK-Seam über IOrganizationService (ersetzt DataverseClient)
  Domain/                        Source-of-Truth-Typen (Config, Conditions, Metadata,
                                 Import, Issues, Template, Enums) – 1:1 aus src/domain/*
  Services/
    MetadataService.cs           RetrieveEntity/AllEntities → normalisiert, Lookup-Targets
    LookupResolver.cs            Feste Reihenfolge GUID → Business Key → Suchfeld (Kern)
    ConditionCompiler.cs         ConditionGroup → ConditionExpression + Zeitanker + Audit
    ImportRunner.cs              Dry Run · Konfliktentscheidung · Commit (ExecuteMultiple)
    ConfigValidationService.cs   Schema-Drift-Preflight + Fingerprint
    DataExportService.cs         Query + CRM-/Schema-Zeilen (inkl. Business-Key-Anreicherung)
    ConfigBuilder.cs             Config aus Metadaten bauen (Editor)
    ViewService.cs               Gespeicherte Views (savedqueries) + FetchXML-Spalten
    PersistedStore.cs            JSON-Persistenz (Configs/Historie) statt localStorage
    Json.cs                      Deterministische JSON-Settings (Manifest-Hash/Persistenz)
    Excel/                       TemplateColumns · ManifestHash (djb2) ·
                                 ExcelTemplateService/ExcelParserService (ClosedXML)
  App/
    I18n.cs                      DE/EN-Strings (Port von src/i18n.ts)
    AppContainer.cs              Composition Root + Config/Historie/aktiver Lauf
  UI/
    IScreenHost.cs · ScreenControlBase.cs · UiTheme.cs · ConflictKey.cs
    DataPreviewModal.cs          3.3 Daten-Vorschau (CRM-/Schema-Spalten)
    Screens/                     Configs · Editor (4 Tabs) · ImportRun · Conflicts ·
                                 Resolve · History
```

## Screens (1:1 zur Code App)

`Configs` (Startseite) · `Editor` (Assistent, 4 Tabs) · `ImportRun` · `Conflicts` ·
`Resolve` · `History`. Siehe `handoff/xrmtoolbox-plugin-handoff.md` bzw. hier
`xrmtoolbox-plugin-handoff.md` für die vollständige Screen-für-Screen-Spezifikation.

## Build

Voraussetzungen: .NET SDK + .NET Framework 4.8 Targeting Pack (bzw. Visual Studio
mit .NET-Desktop-Workload). Der aktuelle XrmToolBox-Host verlangt net48.

```powershell
dotnet restore
dotnet build -c Release
```

Details, Tests und die net48-/Version-Pin-Hintergründe: [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Release & Deployment

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

## Umsetzungsstand

Vollständig portiert und lauffähig:

- **Domain** 1:1 aus `src/domain/*` (Config, Conditions, Metadata, Import, Issues, Template).
- **Services** portiert: MetadataService, LookupResolver (feste Reihenfolge, nie geraten),
  ConditionCompiler (→ QueryExpression + Zeitanker), ImportRunner (Dry Run, Konflikt-
  entscheidung, Commit via `ExecuteMultipleRequest`), ConfigValidationService (Schema-Drift),
  DataExportService, ExcelTemplate/Parser (ClosedXML), ViewService, PersistedStore.
- **UI** alle sechs Screens + Daten-Vorschau-Modal, Statuskacheln/-farben, DE/EN.
- **Verifiziert:** Plugin lädt in XrmToolBox (MEF-Metadaten, Control-Instanziierung);
  21 Kernlogik-Tests grün (Excel-Round-Trip, Manifest-Hash + Tamper, Condition-Compiler,
  JSON-Persistenz, Status-Klassifikation). Release-Build: 0 Warnungen, 0 Fehler.
- **End-to-End gegen eine echte Dataverse-Org verifiziert** (ClientSecret): Metadaten inkl.
  polymorpher Lookup-Targets + Navigation-Property-Enrichment, Resolver in allen Stufen
  (GUID / Suchfeld / NotFound / Ambiguous), Dry Run, Commit via `ExecuteMultipleRequest`,
  und real geschriebene + gegengelesene `EntityReference`-Bindung. Alle 15 E2E-Tests grün.

Verhalten leerer Lookups (bewusste Abweichung von der Quelle): Eine **komplett leere**
Lookup-Spalte (kein GUID/Business Key/Suchwert) wird als nicht-blockierender Status `Empty`
geführt — die Zeile bleibt schreibbar, der Lookup ungesetzt. Ein *gefüllter, aber nicht
auffindbarer* Wert bleibt weiterhin `NotFound` (blockierend).

Offen: optionaler UI-Feinschliff, Version-Bump-Automatik.
