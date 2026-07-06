# Bedienung

Der Grundgedanke: **Konfiguration zuerst**, dann pro Lauf der Round-Trip
**Export → in Excel bearbeiten → Import**. Lookups werden dabei deterministisch
aufgelöst oder an einen Menschen eskaliert – nie geraten.

## Installation

1. Release bauen/entpacken: Inhalt von `deploy\Plugins\` (siehe
   [PUBLISHING.md](PUBLISHING.md) bzw. `build\package.ps1`) nach
   `%AppData%\MscrmTools\XrmToolBox\Plugins` kopieren – oder das Plugin über den Tool
   Store installieren.
2. XrmToolBox starten, eine **Verbindung** zur Umgebung wählen, **LookupImportPlus**
   öffnen. Die Verbindung kommt vom Host – kein separater Login.

## Die Screens

Links die Navigation: **Job-Konfigurationen · Importläufe · Konfliktkorb · Importhistorie**.

### 1. Job-Konfigurationen (Start)
Übersicht als Karten. Pro Karte: Zieltabelle, Operation, Spalten-/Lookup-Zahl, Version,
Entwurfsstatus. Aktionen: **Export ▾** (Leeres Template / Daten exportieren),
**Bearbeiten**, **Import starten**, **Löschen**. Oben **Neue Konfiguration** und
**Excel importieren** (Datei ohne vorgewählte Config – die Zuordnung kommt aus dem
eingebetteten Manifest).

### 2. Konfigurations-Editor (Assistent)
Vier Tabs; Tabs 2–4 sind gesperrt, bis eine Zielentität gewählt ist.

- **1 · Entität & Quelle** – Zieltabelle wählen (lädt Metadaten), Quelle „Entität" oder
  „gespeicherte Ansicht".
- **2 · Allgemein** – Name, Beschreibung, Operation (create/update/createOrUpdate),
  Standard-Schreibmodus (strict/partial).
- **3 · Spalten** – Attribute filtern (Suche · nur ausgewählte/Lookups/Pflicht/beschreibbare)
  und auswählen, Verwendung setzen. Buttons: Daten-Vorschau, Leeres Template,
  Daten exportieren.
- **4 · Lookups** – pro Lookup-Spalte eine Karte: sichtbare Spalte, GUID-/Business-Key-
  Spalte, Konfliktstrategie, Zieltabelle(n) und **pro Ziel** Suchfeld, Business-Key-
  Attribut und Suchbedingungen (fester Wert / Excel-Spalte / relatives Datum).

Kopf: **Abbrechen · Speichern · Import starten**.

### 3. Importlauf
**XLSX hochladen** → **Konfigurationsprüfung** (Schema-Drift; echte Fehler blockieren,
Warnungen werden angezeigt) → **Dry Run** (jede Zeile klassifiziert). Statuskacheln
**Bereit / Konflikte / Fehler / Zeilen gesamt**. **Schreibmodus** Strict/Partial. Bei
offenen Konflikten führt **Konfliktkorb öffnen** weiter; **Commit** schreibt (Strict
blockiert, bis alles gelöst ist; Partial schreibt die sauberen Zeilen sofort).

### 4. Konfliktkorb
Gruppiert nach Quellwert: Zielfeld, betroffene Zeilen, Kandidatenzahl, Status.
**Auflösen →** (≥1 Kandidat) bzw. **Bearbeiten →** (0 Treffer). Eine Entscheidung kann
für die ganze Gruppe gelten. Nichts wird automatisch geraten.

### 5. Konflikt auflösen
Zeigt die zugrunde liegende **Abfrage** (inkl. aufgelöstem Zeitanker), die
**Kandidatenliste** (mit Deep-Link „Öffnen"), Checkbox „auf alle n Zeilen anwenden".
**Auswahl übernehmen** schreibt die Entscheidung zurück **und protokolliert** sie
(Regel, Kandidaten, gewählte GUID, Benutzer, Zeitpunkt). **Überspringen** markiert die
Zeile(n) bewusst als übersprungen.

### 6. Importhistorie
Alle Läufe mit **eingefrorenem Konfig-Snapshot** und Zählern (Gestartet, Konfiguration
+Version, Modus, Zeilen, Geschrieben, Konflikte, Status) – nachvollziehbar bis zur
Lookup-Entscheidung.

## Der typische Ablauf

1. Konfiguration anlegen (Editor) und speichern.
2. **Leeres Template** exportieren **oder** Daten aus der Quelle exportieren.
3. In Excel bearbeiten. Für sichere Auflösung die technischen Spalten nutzen:
   trägt man die **GUID** ein, gewinnt sie sofort (kein Konflikt). Sonst greift der
   **Business Key**, sonst die **Namenssuche + Bedingungen**.
4. **Import starten**, Datei hochladen, Dry Run prüfen.
5. Konflikte im Korb lösen (oder Werte in Excel korrigieren und neu hochladen).
6. **Commit**. Der Lauf landet mit Snapshot in der Historie.

## Statuswerte (Auszug)

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
