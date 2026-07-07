# Übergabe-Prompt: Blog-Artikel „LookupImportPlus" (wolkenkunde.cloud)

> Diese Datei ist der komplette Auftrag für einen Redaktions-Agenten, der im
> Hauptverzeichnis des Blogs **wolkenkunde.cloud** (dieses Gerät) läuft. Sie ist
> so geschrieben, dass der Agent **kalt** starten kann. Alles unten ist maßgeblich;
> im Zweifel gilt der Quellcode/`docs/USAGE.md` des Plugin-Repos.

---

## ROLLE & ZIEL

Erstelle und veröffentliche einen **ausführlichen, bebilderten Fachartikel (Deutsch)**
über das XrmToolBox-Plugin **LookupImportPlus**. Zwei inhaltliche Schwerpunkte:

1. **Konfigurationen anlegen** – Schritt für Schritt durch den 4-Tab-Editor.
2. **Die verschiedenen Auflösungsverfahren** für Lookups (GUID → Business Key → Suchfeld)
   inkl. Fallthrough-Logik und Konfliktbehandlung.

Zielgruppe: Dataverse-/Power-Platform-Praktiker (Consultants, Admins, Data-Migration).
Ton: sachlich, präzise, praxisnah – am `get_persona` des Blogs ausrichten.

---

## ONLINE-QUELLEN (im Artikel verlinken bzw. für Recherche nutzen)

- **Plugin-Repo (GitHub, maßgeblich):** https://github.com/brunsforge/XMRToolbox_LookupImportPlus
- **Original Power-Apps-Code-App (Portierungsvorlage):** https://github.com/brunsforge/LookupImportPlus
- **NuGet-Paket:** https://www.nuget.org/packages/LookupImportPlus
- **XrmToolBox Tool Library:** im XrmToolBox-Tool-Store nach „LookupImportPlus" suchen
  (der Store scannt nuget.org; Tag „XrmToolBox Plugin")
- **XrmToolBox allgemein:** https://www.xrmtoolbox.com
- **Blog:** wolkenkunde.cloud (aktive Site über die Blog-Tools ermitteln)

## LOKALE FAKTENQUELLE (auf diesem Gerät, verbindlich – nichts erfinden)

Repo-Pfad: `C:\Daten\source\XRMToolboxLookupImportPlus`

| Datei | Inhalt |
|---|---|
| `README.md` | Kurzüberblick, Kernregel, Projektstruktur |
| `docs/USAGE.md` | **Hauptquelle**: Screens, Bedienung, typischer Ablauf, Statuswerte |
| `docs/ARCHITECTURE.md` | Schichten, Mapping Code-App → Dataverse-SDK |
| `CHANGELOG.md` | Funktionsumfang je Version (aktuell **0.1.9**) |
| `src/LookupImportPlus/Services/LookupResolver.cs` | **Maßgebliche** Auflösungslogik (Docstring = exakte Reihenfolge) |
| `src/LookupImportPlus/App/I18n.cs` | Exakte deutsche UI-Beschriftungen – **wörtlich** übernehmen |
| `assets/icon-512.png` | Tool-Icon in hoher Auflösung (fürs Artikelbild/Teaser) |

**Lies `docs/USAGE.md` und den `LookupResolver.cs`-Docstring, bevor du schreibst.**

---

## WAS DAS TOOL IST

Auditierbarer **Excel-Import in Microsoft Dataverse** als XrmToolBox-Plugin
(WinForms, .NET Framework 4.8). Grundgedanke: **Konfiguration zuerst**, dann pro Lauf
der Round-Trip **Export → in Excel bearbeiten → Import**.

**Kernregel:** Lookups (Verweise auf verknüpfte Datensätze) werden **deterministisch
aufgelöst oder an einen Menschen eskaliert – nie geraten.** Jede Entscheidung wird
protokolliert (Attribut `lip_resolutiondecision`: Regel, Kandidaten, gewählte GUID,
Benutzer, Zeitpunkt).

Portierung einer Power-Apps-Code-App auf das Dataverse-**SDK** (statt Web API).

---

## DIE VERSCHIEDENEN VERFAHREN  ← Kernabschnitt des Artikels

Feste Matching-Reihenfolge, **erster Treffer gewinnt**, Kaskade mit **Fallthrough**,
je Excel-Zeile datengesteuert:

```
1) GUID-Spalte   →   2) Business Key   →   3) Suchfeld (+ Bedingungen)
```

1. **GUID-Spalte** – Ist die Zelle mit einer gültigen GUID gefüllt, wird der Datensatz
   direkt per Id geladen und der Zieltyp geprüft → **exakter Treffer, fertig**, kein
   Konflikt möglich. Leer/ungültig/nicht gefunden → weiter zu Schritt 2.
   (Bei **polymorphen** Lookups fixiert die Typ-Spalte das Ziel, z. B. account vs. contact.)
2. **Business Key** – Hat die BK-Spalte einen Wert, wird das Ziel gesucht mit
   «BK-Attribut = Zellwert». **Genau 1** Treffer → fertig. **0** → weiter zu Schritt 3.
   **Mehrere** → Konflikt (es wird **nicht** weitergesucht, sondern eskaliert).
3. **Suchfeld (+ Bedingungen)** – Der sichtbare Excel-Wert wird gegen das Suchfeld des
   Ziels geprüft, plus optionale Suchbedingungen. **1** → fertig · **0** → nicht gefunden ·
   **mehrere** → Konflikt.

**Wichtig unbedingt betonen:**
- **„Alle Zellen leer" ≠ „nicht gefunden".** Sind GUID-, BK- und Sicht-Zelle alle leer,
  bleibt der Lookup **unbesetzt** (nicht-blockierend) – die Zeile ist trotzdem schreibbar.
  Ein *vorhandener, aber nicht auffindbarer* Wert wird dagegen zu **Nicht gefunden**.
- **Mehrdeutigkeit wird nie geraten**, sondern gemäß **Konfliktstrategie** behandelt:
  `escalate` (in den Konflikt-Screen), `skip` (Zeile überspringen) oder `fail` (Zeile
  schlägt fehl).

**Technische Excel-Spalten je Lookup** (fürs Verständnis der Vorlage): zu einer
sichtbaren Lookup-Spalte „Firma" gehören die technischen Spalten **„Firma Id"** (GUID),
**„Firma Type"** (Zieltyp bei polymorph) und optional **„Firma Number"** (Business Key).
Trägt der Nutzer die GUID ein, gewinnt sie sofort.

---

## KONFIGURATION ANLEGEN – Screen für Screen

Navigation links: **Job-Konfigurationen · Importläufe · Konflikte · Importhistorie**.
(Hinweis: In der aktuellen Version 0.1.9 heißt der Bereich in der UI **„Konflikte"**;
ältere Doku spricht noch vom „Konfliktkorb" – gemeint ist dasselbe.)

### Start: Job-Konfigurationen
Übersicht als Karten (Zieltabelle, Operation, Spalten-/Lookup-Zahl, Version,
Entwurfsstatus). Aktionen je Karte: **Export ▾** (Leeres Template / Daten exportieren),
**Bearbeiten**, **Import starten**, **Löschen**. Oben: **Neue Konfiguration** und
**Excel importieren** (Datei ohne vorgewählte Config – Zuordnung kommt aus dem
eingebetteten Manifest). *Hinweis:* Export ist erst möglich, wenn **mindestens eine
Spalte** ausgewählt ist.

### Editor – 4 Tabs (Tabs 2–4 gesperrt, bis eine Zielentität gewählt ist)

- **1 · Entität & Quelle** – Zieltabelle wählen (lädt Metadaten). Quelle „Entität"
  oder **„gespeicherte Ansicht"** (savedquery). Die gewählte Ansicht wird gespeichert
  und beim Wiederöffnen wiederhergestellt.
- **2 · Allgemein** – Name, Beschreibung, **Operation** (create / update / createOrUpdate),
  **Standard-Schreibmodus** (strict / partial).
- **3 · Spalten** – Attribute filtern (Suche · nur ausgewählte / Lookups / Pflicht /
  beschreibbare), auswählen und **Verwendung** setzen (ImportExport / ExportOnly /
  ImportOnly). Buttons: **Daten-Vorschau**, **Leeres Template**, **Daten exportieren**.
  Über der Liste der Hinweis: der Datensatz-Schlüssel **`lip__recordid`** wird bei
  Update/Upsert **automatisch** angehängt und ist bewusst nicht auswählbar.
- **4 · Lookups** – pro Lookup-Spalte eine **Karte**:
  - **Sichtbare Excel-Spalte**, **GUID-Spalte**, **Business-Key-Spalte (optional)**
  - **Konfliktstrategie** (Escalate / SkipRow / FailRow)
  - **Zieltabelle(n), in denen gesucht wird** (bei polymorphen Lookups mehrere)
  - **pro Ziel:** **Suchfeld (auf dem Ziel)**, **Business-Key-Attribut (optional)** und
    **Suchbedingungen** – jede Bedingung mit Attribut, Operator und Wertquelle
    (**fester Wert** / **Excel-Spalte** / **relatives Datum in Tagen**).
  - *Neu in 0.1.9:* Suchfeld und Business-Key-Attribut sind **Auswahllisten echter
    Ziel-Attribute** (kein Freitext); **unvollständige Bedingungen werden nicht
    gespeichert** (halb ausgefüllte Zeilen bleiben amber markiert, aber unpersistiert).

Kopfleiste: **Abbrechen · Speichern · Import starten**.

### Importlauf
**XLSX hochladen** → **Konfigurationsprüfung** (Schema-Drift; echte Fehler blockieren,
Warnungen werden angezeigt) → **Dry Run** (jede Zeile klassifiziert). Statuskacheln
**Bereit / Konflikte / Fehler / Zeilen gesamt**. **Schreibmodus** Strict/Partial.
**Konflikte öffnen** führt zur Liste; **Commit** schreibt (Strict blockiert, bis alles
gelöst ist; Partial schreibt die sauberen Zeilen sofort, via ExecuteMultiple).

### Konflikte (Liste)
Gruppiert nach **Quellwert**: Zielfeld, betroffene Zeilen, Kandidatenzahl, Status.
**Auflösen →** (≥1 Kandidat) bzw. **Bearbeiten →** (0 Treffer). Eine Entscheidung kann
für die **ganze Gruppe** gelten. Nichts wird automatisch geraten.

### Konflikt auflösen
Zeigt die zugrunde liegende **Abfrage** (inkl. aufgelöstem Zeitanker), die
**Kandidatenliste** (mit Deep-Link „Öffnen"), Checkbox „auf alle n Zeilen anwenden".
**Auswahl übernehmen** schreibt die Entscheidung zurück **und protokolliert** sie.
**Überspringen** markiert die Zeile(n) bewusst als übersprungen.

### Importhistorie
Alle Läufe mit **eingefrorenem Konfig-Snapshot** und Zählern (Gestartet, Konfiguration
+ Version, Modus, Zeilen, Geschrieben, Konflikte, Status) – nachvollziehbar bis zur
einzelnen Lookup-Entscheidung.

---

## TYPISCHER ABLAUF (als roter Faden für den Artikel)

1. Konfiguration anlegen (Editor) und speichern.
2. **Leeres Template** exportieren **oder** Daten aus der Quelle exportieren.
3. In Excel bearbeiten. Für sichere Auflösung die technischen Spalten nutzen:
   GUID gewinnt sofort; sonst Business Key; sonst Namenssuche + Bedingungen.
4. **Import starten**, Datei hochladen, **Dry Run** prüfen.
5. Konflikte lösen (oder Werte in Excel korrigieren und neu hochladen).
6. **Commit**. Der Lauf landet mit Snapshot in der Historie.

## STATUSWERTE (Auszug – gute Tabelle für den Artikel)

| Status | Bedeutung |
|---|---|
| Bereit / Aufgelöst | schreibbar |
| Mehrdeutig | mehrere Kandidaten → Entscheidung nötig |
| Nicht gefunden | Wert angegeben, aber kein Treffer |
| Pflichtfeld fehlt | Validierung blockiert |
| Übersprungen | bewusst ausgelassen |
| Geschrieben / Schreiben fehlgeschlagen | Commit-Ergebnis |

Eine **leere** Lookup-Spalte blockiert nicht: der Lookup bleibt ungesetzt, die Zeile
ist trotzdem schreibbar.

---

## BERECHTIGUNGS-HINWEIS (unbedingt aufnehmen – häufige Stolperfalle)

Der verbundene Benutzer bzw. **Anwendungsbenutzer** braucht ausreichende Leserechte auf
**Organisationsebene** für Ziel- und Referenztabellen. Die Rolle **„System Customizer"
allein reicht nicht** – sie darf Zieltabellen wie account/contact nur auf **Benutzer**-
Ebene lesen (nur eigene Datensätze), weshalb Abfragen **0 Datensätze** liefern, obwohl
im Web-Client Daten sichtbar sind. Abhilfe: dem App-Benutzer eine Rolle mit
Organisations-Lesetiefe (bzw. **System Administrator**) geben – oder interaktiv als
voll berechtigter Benutzer verbinden.

---

## SCREENSHOTS (Pflicht – der Artikel ist bebildert)

Benötigte Motive, sauber zugeschnitten, echte Kundendaten anonymisiert, **Alt-Text je Bild**:

1. Tool-Kachel/Icon in der XrmToolBox-Tool-Liste (Icon: Grid → Pfeil → Dataverse + Lupe).
2. **Job-Konfigurationen** (mehrere Konfig-Karten mit Aktionen).
3. Editor **Tab 1** (Entität & Quelle) und **Tab 2** (Allgemein).
4. Editor **Tab 3** (Spalten) mit Filterleiste, Grid und dem `lip__recordid`-Hinweis.
5. Editor **Tab 4** (Lookups) – die **Lookup-Karte** mit account/contact-Zielen,
   Suchfeld-Dropdown, Business-Key-Attribut und Bedingungs-Grid. **Wichtigstes Bild.**
6. **Import-Lauf** mit Statuskacheln (Bereit/Konflikte/Fehler/Zeilen) nach dem Dry Run.
7. **Konflikte**-Liste und **Konflikt auflösen** (Abfrage + Kandidatenliste).
8. **Importhistorie** mit Snapshot/Zählern.

**Wie erzeugen (in dieser Reihenfolge bevorzugen):**
- **a) Live:** XrmToolBox auf diesem Gerät starten, Plugin **LookupImportPlus** öffnen,
  mit einer **Dev-/Trial-Umgebung** verbinden und die Screens per Fenster-Screenshot
  aufnehmen (Snipping-Tool oder PowerShell/.NET `Graphics.CopyFromScreen`). Falls eine
  Verbindung/Steuerung nötig ist, **den Nutzer kurz um Hilfe bitten**.
- **b) Fallback (isolierte Lookup-Karte):** Es existiert im Projekt ein WinForms-Render-
  Harness-Muster, das einzelne Controls **headless als PNG** rendert (siehe frühere
  Session-Artefakte `…\scratchpad\…\card2.png`, `tab4b.png`). Damit lässt sich die
  Lookup-Karte deterministisch und ohne echte Daten abbilden.

---

## VERÖFFENTLICHEN (Blog-Tooling von wolkenkunde.cloud)

1. Aktive Site prüfen/setzen (Site-Liste / aktive Site / ggf. setzen).
2. **Ton/Stil an `get_persona` ausrichten**; passende **Kategorie** aus der Kategorienliste.
3. **Bilder hochladen** (Upload-Slot anlegen → finalisieren) und im Artikel einbetten,
   jeweils mit Alt-Text.
4. Artikel **als Entwurf** anlegen: aussagekräftiger **Titel**, **SEO-Beschreibung**,
   sinnvolle Zwischenüberschriften, die Statuswert- und Verfahrens-Tabellen, Screenshots,
   und die **Online-Quellen** (Repo, NuGet, Tool Store) als Links.
5. **Nicht sofort live schalten** – Entwurf zur Freigabe belassen.

## CONSTRAINTS

- **Deutsch**, sachlich, für Dataverse-/Power-Platform-Praktiker.
- **Keine erfundenen Funktionen** – nur was in `docs/USAGE.md`/Code steht (Stand 0.1.9).
- Kernbotschaft nicht verwässern: **deterministisch auflösen oder eskalieren – nie geraten.**
- Am Ende **kurze Rückmeldung an den Auftraggeber**: Titel, Entwurf-URL/-ID, Liste der
  verwendeten Screenshots, offene Punkte.
