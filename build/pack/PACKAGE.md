# LookupImportPlus

Ein **XrmToolBox-Plugin** für den auditierbaren Excel-Import in Dataverse, bei dem
**Lookups deterministisch aufgelöst oder an einen Menschen eskaliert werden — nie
geraten**.

## Kernregel

Feste Matching-Reihenfolge, erster Treffer gewinnt:

```
1) GUID-Spalte  →  2) Business Key  →  3) Suchfeld + Bedingungen
```

0 Treffer ⇒ Nicht gefunden · genau 1 ⇒ aufgelöst · mehrere ⇒ Mehrdeutig →
Konfliktstrategie (eskalieren / überspringen / fehlschlagen).

## Funktionen

- Versionierte Job-Konfigurationen; jeder Lauf speichert einen unveränderlichen Snapshot.
- Round-Trip: Konfigurieren → Export (Template/Daten) → in Excel bearbeiten → Import
  (Hochladen → Schema-Drift-Prüfung → Dry Run → Konflikte lösen → Commit).
- Polymorphe Lookups (z. B. Kunde = Konto|Kontakt) mit Suchfeld/Business-Key/Bedingungen
  pro Zieltabelle.
- Konflikt-Audit: jede Entscheidung wird protokolliert (Regel, Kandidaten, gewählte GUID,
  Benutzer, Zeitpunkt).
- Bulk-Writes über `ExecuteMultipleRequest`.

## Installation

In XrmToolBox über die **Tool Library** installieren (Suche „LookupImportPlus") oder die
DLLs manuell in den `Plugins`-Ordner kopieren.

Projekt & Doku: <https://github.com/brunsforge/XMRToolbox_LookupImportPlus>
