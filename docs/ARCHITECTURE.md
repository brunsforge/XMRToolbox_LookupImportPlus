# Architektur

LookupImportPlus ist ein **faithful Port** der Power-Apps-Code-App
[brunsforge/LookupImportPlus](https://github.com/brunsforge/LookupImportPlus) (TypeScript)
auf ein **XrmToolBox-Plugin** (WinForms, .NET Framework 4.8). Getauscht wurde der
**Transport**: statt der Web API spricht das Plugin das Dataverse-**SDK**
(`IOrganizationService`). Die Domänen- und Ablauflogik ist 1:1 übernommen.

> Source of Truth bei fachlicher Unklarheit ist der TS-Quellcode: `src/domain/*`,
> `src/services/*`, `src/ui/screens/*`, `src/i18n.ts`.

## Schichten

```
UI (WinForms)                LookupImportPlusControl (Shell) + Screens + DataPreviewModal
   │  IScreenHost (Navigation, WorkAsync, Notify)
App                          AppContainer (Composition Root), I18n
   │
Services                     MetadataService · LookupResolver · ConditionCompiler
                             ImportRunner · ConfigValidationService · DataExportService
                             ViewService · PersistedStore · Excel/*
   │
Data                         DataverseContext  (Seam über IOrganizationService)
   │
Domain                       reine Typen (Config, Conditions, Metadata, Import, Issues, Template)
```

Eine Abhängigkeit zeigt nur nach unten. Die **einzige** Stelle mit SDK-I/O ist
`DataverseContext`; alles darüber ist testbar ohne echte Verbindung.

## Transport-Mapping (Code App → SDK)

| Code App (Web API / OData) | Plugin (SDK) |
|---|---|
| `getEntityMetadata` | `RetrieveEntityRequest` (`Attributes\|Relationships`) |
| Entitätsliste | `RetrieveAllEntitiesRequest` (`EntityFilters.Entity`) |
| Polymorphe Ziele + Nav-Property | `ManyToOneRelationships` → `LookupTarget` |
| `name eq '…' and …` (OData) | `QueryExpression` + `ConditionExpression` |
| `…@odata.bind` | `entity[attr] = new EntityReference(logical, id)` |
| Zeilenweise Writes | `ExecuteMultipleRequest` (Batch, ContinueOnError) |
| FetchXML→OData-Übersetzung | entfällt – `FetchExpression` nativ |
| `localStorage` | JSON-Dateien im XrmToolBox-Settings-Ordner (`PersistedStore`) |
| React-Screens | WinForms-`ScreenControlBase`, Navigation via `IScreenHost` |
| Fortschrittsbalken | `WorkAsync` + `ReportProgress` |
| Web-Notifications | `NotifyIcon.ShowBalloonTip` |

## Das Auflösungsmodell (Kern)

Pro Zeile und Lookup, feste Reihenfolge – **nie geraten**:

1. **GUID-Spalte** gefüllt → `Retrieve` nach Id, Zieltyp prüfen → binden.
2. **Business-Key-Spalte** → Query auf das Business-Key-Attribut (pro Ziel).
   genau 1 → binden · 0 → weiter · >1 → sollte eindeutig sein.
3. **Suchfeld + Bedingungen** → `QueryExpression`.
   genau 1 → binden · 0 → `NotFound` · >1 → `Ambiguous` → Konfliktstrategie.

Zusätzlich: eine **komplett leere** Lookup-Spalte ⇒ Status `Empty` (nicht blockierend,
Lookup bleibt ungesetzt) – bewusste Abweichung von der Vorlage.

Bedingungen werden nie als Filter-String gespeichert, sondern als strukturierter
`ConditionGroup`-Baum und erst zur Laufzeit vom `ConditionCompiler` zu
`ConditionExpression`s kompiliert (relative Datumsanker werden dabei aufgelöst und
für das Audit protokolliert).

## Excel-Contract

Der Export erzeugt neben den sichtbaren Spalten **technische Lookup-Spalten**
(`… Id`, `… Type`, `… Number`) und ein **verstecktes Blatt `_LookupImportPlus`** mit
einem serialisierten Manifest (Config-Snapshot + Version + djb2-Integritätshash). Der
Import liest das Manifest zurück (nicht die sichtbaren Header allein), prüft den Hash
und den Schema-Drift, bevor der Dry Run läuft.

## Persistenz & Versionierung

Konfigurationen sind **versioniert**; jeder Lauf speichert einen **unveränderlichen
Snapshot** der verwendeten Konfiguration in der Historie. So bleibt jede
Lookup-Entscheidung bis zur Zeile nachvollziehbar, auch wenn die Config später geändert
wird.

Details zum Datenmodell und den optionalen `lip_*`-Audit-Tabellen: siehe die
`docs/data-model.md` im Quell-Repo.
