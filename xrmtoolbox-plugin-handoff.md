# Übergabeprompt: LookupImportPlus als XrmToolBox-Plugin (WinForms)

Du baust **LookupImportPlus** als **XrmToolBox-Plugin** nach — dieselben Menüs und
Workflows wie die bestehende Power Apps **Code App**, so nah wie es die WinForms-/
XrmToolBox-Umgebung erlaubt. Dieses Dokument beschreibt **alle klickbaren Prozesse**
der UI, was du fachlich über die Anwendung wissen musst, und wie jedes Element auf
WinForms + das Dataverse-**SDK** (statt Web API) abzubilden ist.

Die **maßgebliche Logik-Spezifikation ist der offene Quellcode**:
**https://github.com/brunsforge/LookupImportPlus** — insbesondere `src/domain/*`
(Typen = Source of Truth) und `src/services/*`. Portiere die Domänenlogik von dort;
dieses Dokument sagt dir, *was* die UI tut und *wie* du den Transport tauschst.

---

## 0. Was die Anwendung fachlich macht (must-know)

**Zweck:** auditierbarer Excel-Import in Dataverse, bei dem **Lookups nie geraten**
werden. Der Standard-Import löst Lookups nur über den Anzeigenamen auf und nimmt bei
Mehrdeutigkeit still den ersten Treffer. LookupImportPlus löst **deterministisch**
auf oder **eskaliert an einen Menschen**.

**Nicht verhandelbare Kernregeln (die musst du 1:1 erhalten):**
1. **Feste Matching-Reihenfolge, erster Treffer gewinnt:**
   `1) GUID-Spalte → 2) Business Key → 3) Suchfeld + Bedingungen`.
   0 Treffer ⇒ **NotFound**; genau 1 ⇒ **aufgelöst**; mehrere ⇒ **Mehrdeutig** →
   Konfliktstrategie (`escalate`/`skip`/`fail`). **Nie raten.**
2. **Konfiguration zuerst, versioniert.** Man startet nicht bei Excel, sondern bei
   einer **Job-Konfiguration** (Tabelle + Spalten + Lookup-Auflösungsregeln). Jede
   ist **versioniert**; jeder Lauf speichert einen **unveränderlichen Snapshot** der
   verwendeten Konfiguration.
3. **Round-Trip:** Konfigurieren → Export (Template/Daten) → in Excel bearbeiten →
   Import (Hochladen → Dry Run → Konflikte lösen → Commit).
4. **Polymorphe Lookups:** ein Lookup kann mehrere Zieltabellen haben
   (`customerid` = account|contact); Suchfeld/Business-Key/Bedingungen werden
   **pro Zieltabelle** gesetzt.
5. **Schema-Drift-Preflight:** vor jedem Lauf wird die gespeicherte Konfiguration
   gegen die **aktuellen** Metadaten geprüft; echte Fehler blockieren, Warnungen
   werden nur angezeigt.
6. **Audit:** jede Konfliktentscheidung wird protokolliert (Regel, gezeigte
   Kandidaten, gewählte GUID, Benutzer, Zeitpunkt).

---

## 1. XrmToolBox-/WinForms-Grundgerüst

### UI-Stack: WinForms ist vorgegeben (mit modernen Optionen)
WinForms ist hier **keine freie Wahl, sondern strukturell vorgeschrieben**: Die
Basisklasse `PluginControlBase` (Namespace `XrmToolBox.Extensibility`) **leitet von
`System.Windows.Forms.UserControl` ab**, und der XrmToolBox-Host selbst ist eine
**WinForms-Anwendung auf .NET Framework** (Zielframework beim Plugin: **4.6.2**,
kompatibel bis 4.8). Es gibt kein natives WPF-/WinUI-/Blazor-Host-Modell — dein
Plugin ist immer ein WinForms-`UserControl`.

„Moderner" wird die Oberfläche nur **innerhalb** dieser Hülle:
- **WPF im WinForms-Control** via `ElementHost` (WinForms↔WPF-Interop) — erlaubt
  moderne WPF-Controls/XAML/Data-Binding im Plugin-Panel. Gängiger Weg, wenn du
  über das WinForms-Standard-Look-and-feel hinaus willst.
- **WebView2** (Edge/Chromium) einbetten und die UI als lokales Web-UI (HTML/JS,
  ggf. sogar dieselben React-Komponenten der Code App) rendern; Bridge via
  `postMessage`/`CoreWebView2`. Am nächsten an der Optik der Web-App, aber
  aufwändigere Interop-Brücke zum `IOrganizationService`.
- **Reines WinForms** (`DataGridView`, `TabControl`, Panels) — robust, am wenigsten
  Reibung, empfohlen als Baseline; die untenstehenden Mappings gehen davon aus.

Empfehlung: Baseline **WinForms**; wenn Optik-Parität zur Web-App gefordert ist,
einzelne Panels als **WPF via ElementHost** oder das ganze Content-Panel als
**WebView2** — beides bleibt im WinForms-Shell und ändert die SDK-Anbindung nicht.

- Plugin = `PluginControlBase` (UserControl), plus `IXrmToolBoxPlugin` (Factory) und
  idealerweise `IGitHubPlugin`, `IPayPalPlugin`, `IHelpPlugin`. Nutze das
  **PluginBase-Template** (NuGet `XrmToolBoxPackage`).
- **Verbindung kommt vom Host:** `this.Service` (`IOrganizationService`) ist bereits
  authentifiziert — **kein** interaktiver Power-Apps-/Entra-Login nötig (großer
  Vorteil gegenüber der Code App). `ConnectionDetail` liefert `WebApplicationUrl`
  (für Deep-Links) und `OrganizationServiceProxy`/`ServiceClient`. Umschließe alle
  Serviceaufrufe mit `ExecuteMethod(...)`, damit der Host eine fehlende Verbindung
  abfängt.
- **Alle langlaufenden Operationen** (Metadaten laden, Dry Run, Commit, Export) über
  `WorkAsync(new WorkAsyncInfo { Message, Work, ProgressChanged, PostWorkCallBack })`
  ausführen — nie den UI-Thread blockieren. Das ersetzt die Fortschrittsbalken der
  Web-App (`ReportProgress` → determinierter Balken, Zeilenzahl ist vorab bekannt).
- **Theme:** XrmToolBox liefert ein eigenes Theming. Biete Light/Dark „sofern
  möglich" über den Host-Theme; ein eigener Mond-/Sonne-Umschalter wie in der
  Web-App ist optional. Die Web-App hat oben rechts Sprache (DE/EN) + Theme-Toggle;
  Sprache kannst du analog über eine kleine ComboBox/ToolStrip abbilden.
- **Persistenz:** Die Web-App hält Konfigurationen + Historie im `localStorage`.
  Äquivalent: **`SettingsManager.Instance.Save/Get`** (pro-Plugin JSON) für
  Job-Konfigurationen und Historie. (Phase 2: optional in Dataverse-Tabellen
  `lip_jobconfiguration`/`lip_importjob` spiegeln — siehe `docs/data-model.md`.)

### Transport-Mapping Code App → SDK (zentral!)
| Web App (Code App) | XrmToolBox-Plugin (SDK) |
|---|---|
| Metadaten via SDK `getEntityMetadata` | `RetrieveEntityRequest` (`EntityFilters.Attributes\|Relationships`), Entitätsliste `RetrieveAllEntitiesRequest` (EntityFilters.Entity) |
| Polymorphe Ziele | `LookupAttributeMetadata.Targets` |
| Suche `name eq '…' and …` (OData) | `QueryExpression`/`FetchExpression` + `ConditionExpression` |
| Lookup schreiben `…@odata.bind` | `entity[attr] = new EntityReference(logicalName, guid)` |
| create/update/createOrUpdate | `CreateRequest`/`UpdateRequest`; createOrUpdate → `UpsertRequest` (Alternate Key) bzw. Retrieve-dann-Create/Update |
| Business Key | **Alternate Key** (`EntityMetadata.Keys`) → `KeyAttributeCollection`, Retrieve-by-Key oder Upsert |
| Zeilenweise Schreibvorgänge (Limit der Code App) | **Vorteil hier:** `ExecuteMultipleRequest` / `CreateMultipleRequest`/`UpdateMultipleRequest` für Batches |
| View/FetchXML-Quelle | `savedquery` laden, `FetchExpression` ausführen; FetchXML-Bedingungen direkt nutzbar (keine OData-Übersetzung nötig) |
| Deep-Link zum Datensatz | `{WebApplicationUrl}/main.aspx?etn={etn}&id={guid}&pagetype=entityrecord` |

> Hinweis: Die Web-App muss FetchXML→OData übersetzen (`src/services/fetchxml*`,
> `conditionCompiler`). Im SDK entfällt das großteils — du bleibst nativ bei
> QueryExpression/FetchXML. Die **Bedingungslogik** (fester Wert / andere
> Excel-Spalte / relatives Datum) musst du aber genauso umsetzen: siehe
> `src/services/conditionCompiler.ts` und `src/domain/conditions.ts`.

---

## 2. Gesamtlayout (Shell)

Web-App: linke Sidebar (228 px) + Header + Main.
- **WinForms:** Root `SplitContainer` (oder `TableLayoutPanel`): links Nav-Panel,
  rechts Content-Panel, das je nach Screen ein UserControl hostet.
- **Nav-Einträge (identisch):** `Job-Konfigurationen`, `Importläufe`,
  `Konfliktkorb` (mit Badge = Anzahl offener Konflikte), `Importhistorie`.
  → als Buttons/ToolStrip oder linke `ListView`. Aktiver Eintrag hervorgehoben.
- **Header rechts:** Sprache (DE/EN), Theme-Toggle (optional), Avatar-/Einstellungs-
  Menü (Desktop-Benachrichtigungen — in WinForms via `NotifyIcon.ShowBalloonTip`
  bei Importende).
- **Status-Badge unten links:** „Verbunden mit <org>" statt „Demo-Daten (offline)".

**Screens (UserControls), 1:1 zur Web-App:**
`configs` (Liste) · `editor` (Assistent) · `importrun` · `conflicts` · `resolve` ·
`history`. Navigation zwischen ihnen wie in `src/app/AppContext.tsx` (`ScreenName`,
`navigate(screen, params)`).

---

## 3. Screen für Screen — alle klickbaren Prozesse

### 3.1 Job-Konfigurationen (Startseite, `configs`)
Web-App: Liste von Karten; oben `+ Neue Konfiguration`, `Excel importieren`.

- **WinForms:** `FlowLayoutPanel` mit „Karten" (je ein `Panel`/UserControl) für
  Parität, oder `DataGridView`. Pro Karte anzeigen: Zieltabelle, Operation
  (`create`/`update`/`createOrUpdate`), „N Spalten · M Lookup(s)", Version,
  Entwurfsstatus.
- **Buttons pro Karte (identisch):**
  - **Export ▾** → Dropdown (`ContextMenuStrip`): „Leeres Template" · „Daten
    exportieren (aus Quelle)". → siehe 3.4 (Export).
  - **Bearbeiten** → öffnet Editor (3.2) mit dieser Konfiguration.
  - **Import starten** → öffnet Importlauf (3.5) mit dieser Konfiguration.
  - **Löschen** → `MessageBox` mit Bestätigung, dann entfernen.
- **Kopf-Buttons:** **Neue Konfiguration** → Editor leer; **Excel importieren** →
  eine bereits gefüllte Datei direkt in den Importlauf laden (Datei-Dialog).

### 3.2 Konfigurations-Editor (Assistent, `editor`)
Web-App: `TabControl` mit vier nummerierten Tabs; **Tabs 2–4 sind gesperrt, bis eine
Zielentität gewählt ist**. Kopf-Buttons: **Abbrechen**, **Speichern**,
**Import starten**.

- **WinForms:** `TabControl` mit `TabPage`s; Tabs 2–4 `Enabled=false` bis Entität
  gewählt. Beschrifte Tabs exakt: `1 · Entität & Quelle`, `2 · Allgemein`,
  `3 · Spalten (n)`, `4 · Lookups (n)` (Zähler dynamisch).

**Tab 1 — Entität & Quelle**
- **Zieltabelle wählen** (`ComboBox`, gefüllt aus `RetrieveAllEntitiesRequest`,
  gefiltert auf importierbare/anpassbare Entitäten). Bei Auswahl: Metadaten laden
  (`RetrieveEntityRequest`, Attribute + Relationships) via `WorkAsync`; danach
  Tabs 2–4 freischalten und Anzeige von EntitySet/Primärschlüssel.
- **Quelle:** Radiobutton/ComboBox „Entität selbst" vs. „Gespeicherte View"
  (`savedquery` laden → deren Spalten werden importierbar).

**Tab 2 — Allgemein**
- `TextBox` Name, `TextBox` Beschreibung (mehrzeilig).
- **Operation** (`ComboBox`): create / update / createOrUpdate.
- **Standard-Schreibmodus** (`ComboBox`/Radio): **strict** (nichts schreiben, bis
  alle Zeilen aufgelöst) / **partial** (saubere Zeilen sofort committen).

**Tab 3 — Spalten**
- **Filterleiste:** `TextBox` „Attribute suchen…" + Checkboxen
  `Nur ausgewählte`, `Nur Lookups`, `Nur Pflichtfelder`, `Nur beschreibbare`.
  Rechts Zähler „n ausgewählt".
- **Attributliste:** `DataGridView` mit Checkbox-Spalte (auswählen) + Attribut-
  Anzeigename/Logical Name/Typ; pro gewählter Spalte eine „Nutzung" setzbar.
- **Buttons:** **Daten-Vorschau** (→ 3.3 Modal), **Leeres Template**,
  **Daten exportieren (aus Quelle)** (→ 3.4).

**Tab 4 — Lookups (Kernstück)**
- Oben ein fester **Erklärkasten „Wie ein Lookup aufgelöst wird"** mit den drei
  nummerierten Stufen (GUID / Business Key / Suchfeld) + Warnhinweis. 1:1 übernehmen
  (statischer `Label`/`RichTextBox`).
- **Pro ausgewählter Lookup-Spalte eine Karte** (`Panel`), enthält:
  - Titel „<Anzeigename> → `<logicalname>`" (z. B. `Parent Account → parentcustomerid`).
  - **Sichtbare Excel-Spalte** (`TextBox`) — der menschenlesbare Spaltenname.
  - **Business-Key-Spalte (optional)** (`TextBox`) — Excel-Spalte mit eindeutigem
    Alternativwert (z. B. Kontonummer), greift **vor** der Namenssuche.
  - **Konfliktstrategie** (`ComboBox`): `escalate` / `skip` / `fail`.
  - **Zieltabelle(n)** (`CheckedListBox` aus `LookupAttributeMetadata.Targets`) —
    bei polymorphen Lookups nur die real referenzierten Tabellen ankreuzen.
  - **Pro angekreuztem Ziel** ein Unterblock: **Suchfeld** (`ComboBox` der
    String-Attribute des Ziels, z. B. `account.name`, `contact.fullname`),
    **Business-Key-Attribut** (`ComboBox`, optional; nur Alternate-Key-fähige),
    und **Suchbedingungen**.
  - **Suchbedingungen** (`DataGridView` + „Bedingung hinzufügen" / „remove"): je
    Zeile *linkes Zielfeld* (`ComboBox`), *Operator* (`ComboBox`: eq/ne/ge/le/…),
    *Werttyp* (`ComboBox`: **fester Wert** / **Excel-Spalte derselben Zeile** /
    **relatives Datum (Tage)**), *Wert* (`TextBox`/numeric). Relatives Datum =
    Zeitanker `@utcToday(-Nd)` → im SDK zu konkretem `DateTime` zur Laufzeit
    aufgelöst (siehe `conditionCompiler`).

### 3.3 Daten-Vorschau (Modal)
Web-App: Modal aus dem Spalten-Tab. Toggle **CRM-Spalten** ↔ **Schema-Spalten (mit
generierten)**; Zeilenzahl 10/25/50; farbcodierte Spaltengruppen + Legende; zeigt die
generierten technischen Spalten (`… Id`/GUID, `… Type`, `… Number`).

- **WinForms:** modales `Form` mit `DataGridView`.
  - **Umschalter** (zwei `RadioButton`/ToggleButtons): *CRM-Spalten* = rohe
    Dataverse-Felder; *Schema-Spalten* = das Excel-Layout, das der Export erzeugt.
  - **Zeilen** (`ComboBox` 10/25/50) → `RetrieveMultiple` mit `TopCount`.
  - **Farbcodierte Gruppen:** je Lookup eine Farbe; färbe die `DataGridView`-
    Spaltenkopf-`HeaderCell` und zeige eine Legende (kleine farbige Labels).
    Die Schema-Ansicht muss neben der sichtbaren Spalte die technischen Spalten
    zeigen: `<Lookup> Id` (GUID), `<Lookup> Type` (Zieltyp bei polymorph),
    `<Lookup> Number` (Business Key), plus `lip__recordid`.
- **Schließen**-Button.

### 3.4 Export (Template / Daten)
Web-App: erzeugt XLSX mit **verstecktem Blatt `_LookupImportPlus`** (Config-Snapshot
+ Version) und **versteckten technischen Spalten**.

- **WinForms:** XLSX mit **ClosedXML** oder **EPPlus** erzeugen; `SaveFileDialog`.
  - **Leeres Template:** nur Kopfzeilen (sichtbare + technische Spalten).
  - **Daten exportieren:** echte Datensätze der Quelle (`RetrieveMultiple`/View),
    technische Lookup-Spalten (Id/Type/Number) gefüllt.
  - Verstecktes Blatt `_LookupImportPlus` mit serialisiertem Config-Snapshot +
    Version (Basis für den Konfig-Check beim Reimport). Technische Spalten
    `Hidden=true`.
  - Logik-Vorlage: `src/services/DataExportService.ts`, `src/domain/template.ts`.

### 3.5 Importlauf (`importrun`)
Web-App: **XLSX hochladen** oder **Demo-Datei laden**; zuerst **Konfigurationsprüfung**
(Schema-Drift), dann **Dry Run** (jede Zeile klassifiziert), Statuskacheln
(Bereit/Konflikte/Fehler/Zeilen gesamt), **Schreibmodus Strict/Partial**, dann
**Commit**.

- **WinForms:**
  - **XLSX hochladen** (`OpenFileDialog`) — ClosedXML/EPPlus lesen; verstecktes
    Blatt `_LookupImportPlus` gegen aktuelle Metadaten prüfen (Schema-Drift-
    Preflight, `src/services/ConfigValidationService.ts`). Bei echten Fehlern Lauf
    blockieren (rote Meldung), Warnungen anzeigen. (Optional „Demo-Datei laden" für
    Tests.)
  - **Dry Run** (`WorkAsync` + `ReportProgress`): pro Zeile die Matching-Reihenfolge
    ausführen (`src/services/LookupResolver.ts`), Status vergeben:
    `Ready`, `Ambiguous`, `NotFound`, `MissingRequiredValue`, (weitere siehe
    `src/domain/issues.ts`). **Statuskacheln** als `Panel`s oben; **Ergebnistabelle**
    `DataGridView` mit Spalten Zeile/Schlüsselfelder/Parent-Spalte/„Auflösung"/
    Status (Status farbig via `CellFormatting`).
  - **Schreibmodus** Strict/Partial (zwei ToggleButtons). Bei Strict + offenen
    Konflikten ist Commit gesperrt („Erst Konflikte lösen"); bei Partial erscheint
    **Commit (n)** = Anzahl sauberer Zeilen.
  - **Konfliktkorb öffnen →** navigiert zu 3.6.
  - **Commit** (`WorkAsync`): schreibt via SDK. **Nutze `ExecuteMultipleRequest`**
    (ContinueOnError=true) in Batches à 100–1000; Lookups als `EntityReference`
    setzen; `UpsertRequest` bei createOrUpdate. Per-Zeilen-Fehler isolieren und im
    Ergebnis markieren. Danach Lauf in die **Historie** schreiben (Snapshot + Zähler).
    Logik-Vorlage: `src/services/ImportRunner.ts`.

### 3.6 Konfliktkorb (`conflicts`)
Web-App: **gruppiert nach Quellwert** („1 Zeile sagt Contoso GmbH"); je Gruppe
Zielfeld, betroffene Zeilen, Kandidatenzahl, Status; Button **Auflösen →** bzw.
**Bearbeiten →**; Hinweis „Nichts wird automatisch geraten"; Fußnote zum Audit-Log.

- **WinForms:** `DataGridView` (oder gruppierte Liste) mit Spalten Quellwert,
  Zielfeld (`logicalname`), Betroffen (Zeilen), Kandidaten, Status. Button-Spalte
  **Auflösen →** (bei ≥1 Kandidat) / **Bearbeiten →** (bei 0 Treffern).
  **← Zurück zum Lauf**. Gruppierung nach identischem Quellwert — eine Entscheidung
  gilt für die ganze Gruppe.

### 3.7 Konflikt auflösen (Detail, `resolve`)
Web-App: Kopf „Konflikt auflösen · Quellwert … · Zielfeld …"; Warnbox, warum nicht
eindeutig; **zeigt die genutzte Abfrage** (`name eq '…' and modifiedon ge …`) inkl.
Zeitanker; **Kandidatenliste** (Radio) mit Name, Business Key, `modifiedon`, GUID,
Typ, **Öffnen ↗** (Deep-Link); Checkbox **„Entscheidung auf alle n Zeilen anwenden"**;
Buttons **Überspringen**, **Auswahl übernehmen →**, **Zur Liste**.

- **WinForms:** `Form`/UserControl:
  - Labels für Quellwert + Zielfeld; **Warn-`Panel`** mit Begründung
    (z. B. „GUID- und Business-Key-Spalten waren leer – daher Suche über das
    Namensfeld").
  - **Abfrageanzeige:** read-only `TextBox`/`Label` mit der zugrundeliegenden Query
    (FetchXML/QueryExpression menschenlesbar) + aufgelöstem Zeitanker.
  - **Kandidaten:** Liste aus `RetrieveMultiple`; je Kandidat `RadioButton` + Felder
    (Name, `accountnumber`/Business Key, `modifiedon`, verkürzte GUID, Ziel-Typ) +
    LinkLabel **Öffnen ↗** (Deep-Link-URL, `Process.Start`).
  - **Checkbox „auf alle Zeilen mit diesem Wert anwenden"**.
  - **Auswahl übernehmen →** schreibt die Entscheidung zurück (Zeile(n) auf gewählte
    GUID setzen) **und protokolliert** sie (Regel, Kandidaten, gewählte GUID,
    Benutzer=`WhoAmI`, Zeitpunkt). **Überspringen** markiert bewusst übersprungen.
    **Zur Liste** zurück zum Korb.

### 3.8 Importhistorie (`history`)
Web-App: Tabelle aller Läufe mit **eingefrorenem Konfig-Snapshot** und Zählern
(Gestartet, Konfiguration+Version, Modus, Zeilen, Geschrieben, Konflikte, Status).

- **WinForms:** `DataGridView`, Datenquelle = persistierte Historie
  (`SettingsManager`). Jede Zeile trägt ihren Config-Snapshot (für Nachvollzieh-
  barkeit bis zur Lookup-Entscheidung). Status-Chip farbig.

---

## 4. Das Auflösungsmodell (exakt portieren)

Aus `src/services/LookupResolver.ts` / `src/domain/config.ts`. Pro Zeile und Lookup:

```
1. GUID-Spalte gefüllt?      → Retrieve(EntityReference by id), Zieltyp prüfen → binden. Fertig.
2. Business-Key-Spalte?       → Retrieve-by-Key (Alternate Key) / Query auf Business-Key-Attribut.
                                 genau 1 → binden; 0 → NotFound; >1 → (sollte Alt-Key-unique sein).
3. Suchfeld + Bedingungen     → QueryExpression: <searchAttribute> eq <ExcelWert> + Bedingungen.
                                 genau 1 → binden | 0 → NotFound | >1 → Ambiguous → Konfliktstrategie.
```
- **Polymorph:** Schritte 2–3 pro angekreuzter Zieltabelle mit deren eigenen
  Attributnamen; `… Type`-Spalte entscheidet/merkt den Zieltyp.
- **Bedingungen** (`conditions.ts`): linkes Zielfeld `op` Wert, Wert ∈ {fest,
  Excel-Spalte derselben Zeile, relatives Datum}. Relatives Datum zur Laufzeit
  auflösen (`@utcToday(-Nd)` → `DateTime.UtcNow.Date.AddDays(-N)`).
- **GUID-Round-Trip:** technische `… Id`-Spalte im Export → Nutzer trägt korrekte
  GUID ein → Stufe 1 gewinnt, kein Konfliktscreen.
- Beispiel-Konfig (Zielstruktur, aus dem Artikel):
```json
{ "lookupAttribute":"parentcustomerid","visibleColumn":"Parent Account",
  "guidColumn":"Parent Account Id","businessKeyColumn":"Parent Account Number",
  "targetEntities":["account","contact"],
  "targetOverrides":{"account":{"searchAttribute":"name"},"contact":{"searchAttribute":"fullname"}},
  "conflictStrategy":"escalate" }
```

---

## 5. Was WinForms/XrmToolBox NICHT (leicht) kann — bewusst entscheiden

- **Modales vs. Inline:** Web-Modale (Daten-Vorschau, Resolve) → separate `Form`s.
  Ok, aber achte auf Nicht-Blockieren des Hosts (ShowDialog ist ok, langlaufende
  Abfragen darin trotzdem via WorkAsync/BackgroundWorker).
- **Farbcodierte Legenden / „Karten"-Layout:** in WinForms mehr Handarbeit
  (Custom-Paint / Panels). Priorität mittel — Funktion vor Pixelparität.
- **Theme-Umschalter/Sprache:** nur „sofern möglich"; XrmToolBox-Theme nutzen.
  DE/EN-Umschaltung über Ressourcen (`.resx`) statt eigenem i18n.
- **Desktop-Benachrichtigungen:** `NotifyIcon.ShowBalloonTip` statt Web-Notifications.
- **Determinierte Fortschrittsbalken:** via `WorkAsync`/`ReportProgress` (Zeilenzahl
  vorab bekannt) — gut abbildbar.
- **Persistenz:** `SettingsManager` (JSON) statt `localStorage`; identisches Modell.

---

## 6. Vorteile der SDK-Umgebung (nutzen!)
- **Kein interaktiver Auth-Tanz** — Verbindung kommt vom Host.
- **Bulk-Writes** über `ExecuteMultipleRequest`/`CreateMultipleRequest` — die Code
  App schreibt heute zeilenweise; das Plugin kann von Anfang an batchen.
- **FetchXML/QueryExpression nativ** — keine OData-Übersetzungsschicht nötig.
- **Alternate Keys / Upsert** direkt verfügbar für Business-Key-Matching und
  createOrUpdate.

---

## 7. Empfohlene Umsetzungsreihenfolge
1. Plugin-Gerüst + Verbindung + Entitäts-/Metadaten-Laden (Tab 1).
2. Editor (Tabs 2–4) + Persistenz der Konfiguration.
3. Export (Template/Daten) inkl. verstecktem Blatt + technischen Spalten.
4. Import: Reader + Schema-Drift-Preflight + Dry Run + Resolver (Kern!).
5. Konfliktkorb + Resolve + Audit-Log.
6. Commit (ExecuteMultiple) + Historie.
7. Feinschliff: Daten-Vorschau-Modal, Badges, Benachrichtigungen, Theme/Sprache.

**Source of Truth bleibt der Code:** `src/domain/*` und `src/services/*` in
https://github.com/brunsforge/LookupImportPlus — bei jeder fachlichen Unklarheit dort
nachsehen, nicht neu erfinden. Ziel: identische Menüs und Workflows, wo die
WinForms-/SDK-Umgebung es zulässt.

---

## 8. Öffentliche Quellen

**Die nachzubauende Anwendung (Logik-Spezifikation):**
- LookupImportPlus (Code App, offen): https://github.com/brunsforge/LookupImportPlus
  — maßgeblich: `src/domain/*`, `src/services/*` (LookupResolver, ImportRunner,
  ConfigValidationService, DataExportService, MetadataService, conditionCompiler,
  fetchxmlToOData) sowie `docs/` (ARCHITECTURE, USAGE, data-model, SETUP).

**XrmToolBox — offizielle Entwickler-Quellen:**
- Entwickler-Doku (Übersicht): https://www.xrmtoolbox.com/documentation/for-developers/
- `PluginControlBase` (Basisklasse): https://www.xrmtoolbox.com/documentation/for-developers/plugincontrolbase-base-class/
- `MultipleConnectionsPluginControlBase` (Mehrfach-Verbindungen): https://www.xrmtoolbox.com/documentation/for-developers/multipleconnectionsplugincontrolbase-base-class/
- Eigenes Tool erstellen: https://www.xrmtoolbox.com/documentation/for-developers/create-your-own-plugin-for-xrmtoolbox/
- Projekt-Template installieren (VSIX): https://www.xrmtoolbox.com/documentation/for-developers/install-xrmtoolbox-plugin-project-template/
- Im Tool-Store veröffentlichen: https://www.xrmtoolbox.com/documentation/for-developers/deploy-your-plugin-in-plugins-store/
- GitHub (Host-App + Wiki): https://github.com/MscrmTools/XrmToolBox
  · Wiki „Develop your own custom plugin": https://github.com/MscrmTools/XrmToolBox/wiki/Develop-your-own-custom-plugin-for-XrmToolBox
- NuGet `XrmToolBoxPackage` (Plugin-Referenzen): https://www.nuget.org/packages/XrmToolBoxPackage/

**Dataverse SDK (für den Transport-Tausch):**
- `Microsoft.CrmSdk.CoreAssemblies` / `Microsoft.PowerPlatform.Dataverse.Client`
  (NuGet) — Organization-Service, `ExecuteMultipleRequest`, `UpsertRequest`,
  `RetrieveEntityRequest`, `LookupAttributeMetadata`, Alternate Keys.

> Hinweis zum UI-Stack (siehe §1): WinForms/.NET Framework 4.6.2 ist der Standard und
> strukturell vorgegeben. „Moderner" nur via **ElementHost (WPF)** oder **WebView2**
> innerhalb des WinForms-Shells.
