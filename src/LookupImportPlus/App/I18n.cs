using System.Collections.Generic;
using System.Globalization;

namespace LookupImportPlus.App
{
    public enum Lang { En, De }

    /// <summary>
    /// Minimal i18n (port of src/i18n.ts). Default English; German when the UI
    /// culture is German. UI strings only — Excel column names and Dataverse
    /// logical names are data, not translated.
    /// </summary>
    public static class I18n
    {
        public static Lang Current { get; set; } = DetectLang();

        public static Lang DetectLang()
        {
            var name = CultureInfo.CurrentUICulture?.TwoLetterISOLanguageName ?? "en";
            return name.ToLowerInvariant().StartsWith("de") ? Lang.De : Lang.En;
        }

        public static string T(string key, IDictionary<string, object> parameters = null)
        {
            var s = Dict.TryGetValue(key, out var entry)
                ? (Current == Lang.De ? entry.De : entry.En)
                : key;
            if (parameters != null)
                foreach (var kv in parameters)
                    s = s.Replace("{" + kv.Key + "}", kv.Value?.ToString() ?? "");
            return s;
        }

        public static string T(string key, string paramKey, object paramValue)
            => T(key, new Dictionary<string, object> { [paramKey] = paramValue });

        private struct Entry
        {
            public string En;
            public string De;
            public Entry(string en, string de) { En = en; De = de; }
        }

        private static readonly Dictionary<string, Entry> Dict = new Dictionary<string, Entry>
        {
            ["nav.configs"] = new Entry("Job configurations", "Job-Konfigurationen"),
            ["nav.importruns"] = new Entry("Import runs", "Importläufe"),
            ["nav.conflicts"] = new Entry("Conflicts", "Konflikte"),
            ["nav.history"] = new Entry("Import history", "Importhistorie"),
            ["shell.live"] = new Entry("Dataverse connected", "Dataverse verbunden"),
            ["shell.offline"] = new Entry("Not connected", "Nicht verbunden"),

            ["common.cancel"] = new Entry("Cancel", "Abbrechen"),
            ["common.save"] = new Entry("Save", "Speichern"),
            ["common.back"] = new Entry("Back", "Zurück"),
            ["common.export"] = new Entry("Export", "Export"),
            ["common.edit"] = new Entry("Edit", "Bearbeiten"),
            ["common.startImport"] = new Entry("Start import", "Import starten"),
            ["common.newConfig"] = new Entry("New configuration", "Neue Konfiguration"),
            ["common.delete"] = new Entry("Delete", "Löschen"),
            ["configs.deleteTitle"] = new Entry("Delete configuration?", "Konfiguration löschen?"),
            ["configs.deleteBody"] = new Entry("'{name}' will be permanently removed. This cannot be undone.", "'{name}' wird endgültig entfernt. Das kann nicht rückgängig gemacht werden."),
            ["common.importExcel"] = new Entry("Import Excel", "Excel importieren"),
            ["common.skip"] = new Entry("Skip", "Überspringen"),
            ["common.toList"] = new Entry("To list", "Zur Liste"),
            ["common.close"] = new Entry("Close", "Schließen"),

            ["configs.subtitle"] = new Entry(
                "A configuration describes once how a table is exported and re-imported — including safe lookup resolution. Each run is then: Export → edit in Excel → Import.",
                "Eine Konfiguration beschreibt einmalig, wie eine Tabelle exportiert und wieder importiert wird – inkl. sicherer Lookup-Auflösung. Danach ist jeder Lauf: Export → in Excel bearbeiten → Import."),
            ["configs.entity"] = new Entry("Entity", "Entität"),
            ["configs.operation"] = new Entry("Operation", "Operation"),
            ["configs.columns"] = new Entry("columns", "Spalten"),
            ["configs.lookups"] = new Entry("lookup(s)", "Lookup(s)"),
            ["configs.draft"] = new Entry("Draft", "Entwurf"),

            ["run.title"] = new Entry("Import run", "Importlauf"),
            ["run.targetEntity"] = new Entry("Target entity", "Zielentität"),
            ["run.snapshot"] = new Entry("Snapshot", "Snapshot"),
            ["run.uploadXlsx"] = new Entry("Upload XLSX", "XLSX hochladen"),
            ["run.busyDry"] = new Entry("Dry run – validating rows and resolving lookups…", "Dry Run – Zeilen werden geprüft und Lookups aufgelöst…"),
            ["run.busyRead"] = new Entry("Reading file…", "Datei wird gelesen…"),
            ["run.busyWrite"] = new Entry("Writing to Dataverse…", "Schreiben nach Dataverse…"),
            ["run.rowsUnit"] = new Entry("rows", "Zeilen"),
            ["run.ready"] = new Entry("Ready", "Bereit"),
            ["run.conflicts"] = new Entry("Conflicts", "Konflikte"),
            ["run.errors"] = new Entry("Errors", "Fehler"),
            ["run.totalRows"] = new Entry("Total rows", "Zeilen gesamt"),
            ["run.needDecision"] = new Entry("conflict(s) need a decision.", "Konflikt(e) benötigen eine Entscheidung."),
            ["run.openBasket"] = new Entry("Open conflicts →", "Konflikte öffnen →"),
            ["run.writeMode"] = new Entry("Write mode:", "Schreibmodus:"),
            ["run.commit"] = new Entry("Commit", "Commit"),
            ["run.commitBlocked"] = new Entry("Resolve conflicts first", "Erst Konflikte lösen"),
            ["run.empty"] = new Entry("Upload an edited XLSX to start the dry run.", "Lade eine bearbeitete XLSX hoch, um den Dry Run zu starten."),
            ["run.colRow"] = new Entry("Row", "Zeile"),
            ["run.colResolution"] = new Entry("Resolution", "Auflösung"),
            ["run.colStatus"] = new Entry("Status", "Status"),
            ["run.candidates"] = new Entry("candidates", "Kandidaten"),
            ["run.noMatch"] = new Entry("no match", "kein Treffer"),

            ["conf.subtitle"] = new Entry("All open lookups that need a decision. Identical source values are grouped — one decision can apply to the whole group.", "Alle offenen Lookups, die eine Entscheidung brauchen. Gleiche Quellwerte sind gruppiert – eine Entscheidung kann für die ganze Gruppe gelten."),
            ["conf.backToRun"] = new Entry("← Back to run", "← Zurück zum Lauf"),
            ["conf.allResolved"] = new Entry("Everything resolved", "Alles aufgelöst"),
            ["conf.allResolvedHint"] = new Entry("No open conflicts. Back to the run and commit.", "Keine offenen Konflikte. Zurück zum Lauf und committen."),
            ["conf.needDecisionFull"] = new Entry("lookup(s) need a decision. Nothing is guessed automatically.", "Lookup(s) benötigen eine Entscheidung. Nichts wird automatisch geraten."),
            ["conf.colSource"] = new Entry("Source value", "Quellwert"),
            ["conf.colField"] = new Entry("Target field", "Zielfeld"),
            ["conf.colAffected"] = new Entry("Affected", "Betroffen"),
            ["conf.colCandidates"] = new Entry("Candidates", "Kandidaten"),
            ["conf.rows"] = new Entry("rows", "Zeilen"),
            ["conf.hits0"] = new Entry("0 hits", "0 Treffer"),
            ["conf.resolve"] = new Entry("Resolve →", "Auflösen →"),
            ["conf.editRow"] = new Entry("Edit →", "Bearbeiten →"),
            ["conf.audit"] = new Entry("Every decision is logged (lip_resolutiondecision): rule, candidates, chosen GUID, user, timestamp.", "Jede Entscheidung wird protokolliert (lip_resolutiondecision): Regel, Kandidaten, gewählte GUID, Benutzer, Zeitpunkt."),
            ["conf.noRun"] = new Entry("No active import run. Start an import first.", "Kein aktiver Importlauf. Starte zuerst einen Import."),

            ["res.title"] = new Entry("Resolve conflict", "Konflikt auflösen"),
            ["res.sourceValue"] = new Entry("Source value", "Quellwert"),
            ["res.targetField"] = new Entry("Target field", "Zielfeld"),
            ["res.notOpen"] = new Entry("This conflict is no longer open.", "Dieser Konflikt ist nicht mehr offen."),
            ["res.notUnique"] = new Entry("Lookup not uniquely resolvable", "Lookup nicht eindeutig auflösbar"),
            ["res.noTarget"] = new Entry("No target found", "Kein Ziel gefunden"),
            ["res.affected"] = new Entry("affected row(s). GUID and business-key columns were empty, so the name field was searched.", "betroffene Zeile(n). GUID- und Business-Key-Spalten waren leer – daher Suche über das Namensfeld."),
            ["res.chooseCandidate"] = new Entry("Candidates – please choose the correct one", "Kandidaten – bitte den richtigen wählen"),
            ["res.applyAll"] = new Entry("Apply decision to all {n} row(s) with", "Entscheidung auf alle {n} Zeile(n) anwenden mit"),
            ["res.apply"] = new Entry("Apply selection →", "Auswahl übernehmen →"),
            ["res.open"] = new Entry("Open ↗", "Öffnen ↗"),
            ["res.noCandidates"] = new Entry("No candidates. Correct the value in the Excel file or skip the row.", "Keine Kandidaten. Wert in der Excel-Datei korrigieren oder Zeile überspringen."),
            ["res.skipRows"] = new Entry("Skip rows", "Zeilen überspringen"),
            ["res.timeAnchor"] = new Entry("Time anchor", "Zeitanker"),
            ["res.query"] = new Entry("Query used", "Verwendete Abfrage"),

            ["hist.subtitle"] = new Entry("Every run with a frozen configuration snapshot — traceable down to the row and lookup decision.", "Jeder Lauf mit eingefrorenem Konfigurations-Snapshot – nachvollziehbar bis zur Zeile und Lookup-Entscheidung."),
            ["hist.none"] = new Entry("No runs yet. Start an import.", "Noch keine Läufe. Starte einen Import."),
            ["hist.started"] = new Entry("Started", "Gestartet"),
            ["hist.config"] = new Entry("Configuration", "Konfiguration"),
            ["hist.mode"] = new Entry("Mode", "Modus"),
            ["hist.rows"] = new Entry("Rows", "Zeilen"),
            ["hist.written"] = new Entry("Written", "Geschrieben"),
            ["hist.conflicts"] = new Entry("Conflicts", "Konflikte"),
            ["hist.status"] = new Entry("Status", "Status"),

            ["ed.tabGeneral"] = new Entry("General", "Allgemein"),
            ["ed.tabEntitySource"] = new Entry("Entity & Source", "Entität & Quelle"),
            ["ed.tabColumns"] = new Entry("Columns", "Spalten"),
            ["ed.tabLookups"] = new Entry("Lookups", "Lookups"),
            ["ed.name"] = new Entry("Name", "Name"),
            ["ed.description"] = new Entry("Description", "Beschreibung"),
            ["ed.operation"] = new Entry("Operation", "Operation"),
            ["ed.defaultMode"] = new Entry("Default write mode", "Standard-Schreibmodus"),
            ["ed.targetEntity"] = new Entry("Target entity", "Zielentität"),
            ["ed.select"] = new Entry("— select —", "— auswählen —"),
            ["ed.loadingMeta"] = new Entry("Loading metadata…", "Metadaten werden geladen…"),
            ["ed.entitySetInfo"] = new Entry("Entity set {set} · primary id {id}", "Entity Set {set} · Primär-ID {id}"),
            ["ed.exportSource"] = new Entry("Export source", "Exportquelle"),
            ["ed.sourceEntity"] = new Entry("Entity (all records)", "Entität (alle Datensätze)"),
            ["ed.sourceView"] = new Entry("Saved view", "Gespeicherte Ansicht"),
            ["ed.view"] = new Entry("View", "Ansicht"),
            ["ed.useViewColumns"] = new Entry("Use view columns →", "Spalten der Ansicht übernehmen →"),
            ["ed.lookupsOnly"] = new Entry("Lookups only", "Nur Lookups"),
            ["ed.requiredOnly"] = new Entry("Required only", "Nur Pflichtfelder"),
            ["ed.writableOnly"] = new Entry("Writable only", "Nur beschreibbare"),
            ["ed.selectedOnly"] = new Entry("Selected only", "Nur ausgewählte"),
            ["ed.searchCols"] = new Entry("Search attributes…", "Attribute suchen…"),
            ["ed.selectEntityFirst"] = new Entry("Select a target entity first (step 1).", "Zuerst eine Zielentität wählen (Schritt 1)."),
            ["ed.selected"] = new Entry("selected", "ausgewählt"),
            ["ed.usageImportExport"] = new Entry("Import & Export", "Import & Export"),
            ["ed.usageExportOnly"] = new Entry("Export only", "Nur Export"),
            ["ed.usageImportOnly"] = new Entry("Import only", "Nur Import"),
            ["ed.req"] = new Entry("req", "Pflicht"),
            ["ed.displayName"] = new Entry("Display name", "Anzeigename"),
            ["ed.logicalName"] = new Entry("Logical name", "Logical Name"),
            ["ed.type"] = new Entry("Type", "Typ"),
            ["ed.usage"] = new Entry("Usage", "Verwendung"),
            ["ed.visibleColumn"] = new Entry("Visible Excel column", "Sichtbare Excel-Spalte"),
            ["ed.guidColumn"] = new Entry("GUID column", "GUID-Spalte"),
            ["ed.conflictStrategy"] = new Entry("Conflict strategy", "Konfliktstrategie"),
            ["ed.emptyTemplate"] = new Entry("Empty template", "Leeres Template"),
            ["ed.previewData"] = new Entry("Preview data", "Daten-Vorschau"),
            ["ed.exportData"] = new Entry("Export data (from source)", "Daten exportieren (aus Quelle)"),
            ["ed.needColumns"] = new Entry("Select at least one column before exporting.", "Bitte vor dem Export mindestens eine Spalte auswählen."),
            ["ed.pkAutoIncluded"] = new Entry("The record key (lip__recordid) is added automatically for update/upsert — you don't select it here.", "Der Datensatz-Schlüssel (lip__recordid) wird bei Update/Upsert automatisch angehängt – hier nicht auswählbar."),
            ["ed.lookupIntroTitle"] = new Entry("How each lookup is resolved — top to bottom, per row; the next step runs only if the previous found no single match", "Wie jeder Lookup aufgelöst wird — von oben nach unten, pro Zeile; der nächste Schritt greift nur, wenn der vorige keinen eindeutigen Treffer hatte"),
            ["ed.selectLookupHint"] = new Entry("Select a lookup column in the Columns tab (step 3) to configure it here.", "Wähle im Register Spalten (Schritt 3) eine Lookup-Spalte, um sie hier zu konfigurieren."),
            ["ed.match1"] = new Entry("GUID column filled & valid → exact record, done. Empty → go to step 2. (Type column pins the target for polymorphic lookups.)", "GUID-Spalte gefüllt & gültig → exakter Datensatz, fertig. Leer → weiter zu 2. (Typ-Spalte fixiert bei polymorphen Lookups das Ziel.)"),
            ["ed.match2"] = new Entry("Business key: BK column has a value → find target where «BK attribute = cell value». Exactly 1 → done. 0 → go to step 3.", "Business Key: BK-Spalte hat einen Wert → Ziel mit «BK-Attribut = Zellwert» suchen. Genau 1 → fertig. 0 → weiter zu 3."),
            ["ed.match3"] = new Entry("Search field: the visible Excel value (+ optional conditions) against the target's search field.", "Suchfeld: der sichtbare Excel-Wert (+ optionale Bedingungen) gegen das Suchfeld des Ziels."),
            ["ed.matchConflict"] = new Entry("0 or several matches → conflict (escalate / skip / fail — your choice below). Never guessed.", "0 oder mehrere Treffer → Konflikt (eskalieren / überspringen / fehlschlagen — Wahl unten). Nie geraten."),
            ["ed.conditionsLabel"] = new Entry("Search conditions (on the target)", "Suchbedingungen (auf dem Ziel)"),
            ["ed.addCondition"] = new Entry("Add condition", "Bedingung hinzufügen"),
            ["ed.noConditions"] = new Entry("No conditions — matches on the search field alone.", "Keine Bedingungen — matcht allein über das Suchfeld."),
            ["ed.srcLiteral"] = new Entry("Fixed value", "Fester Wert"),
            ["ed.srcExcel"] = new Entry("Excel column", "Excel-Spalte"),
            ["ed.srcRelative"] = new Entry("Relative date (days)", "Relatives Datum (Tage)"),
            ["ed.searchFieldLabel"] = new Entry("Search field (on the target)", "Suchfeld (auf dem Ziel)"),
            ["ed.bkColumnLabel"] = new Entry("Business key column (optional)", "Business-Key-Spalte (optional)"),
            ["ed.bkFieldLabel"] = new Entry("Business key attribute (optional)", "Business-Key-Attribut (optional)"),
            ["ed.targetEntitiesLabel"] = new Entry("Target table(s) to search", "Zieltabelle(n), in denen gesucht wird"),
            ["ed.perTargetConfig"] = new Entry("Per target: search field, business key & conditions", "Pro Ziel: Suchfeld, Business Key & Bedingungen"),

            ["preview.title"] = new Entry("Data preview", "Datenvorschau"),
            ["preview.crmCols"] = new Entry("CRM columns", "CRM-Spalten"),
            ["preview.schemaCols"] = new Entry("Schema columns (with generated)", "Schema-Spalten (mit generierten)"),
            ["preview.rows"] = new Entry("Rows", "Zeilen"),
            ["preview.empty"] = new Entry("No records found for this source.", "Keine Datensätze für diese Quelle gefunden."),
            ["preview.loading"] = new Entry("Loading preview…", "Vorschau wird geladen…"),
            ["preview.hint"] = new Entry("CRM columns are the raw Dataverse fields; schema columns are the Excel layout this configuration generates.", "CRM-Spalten sind die rohen Dataverse-Felder; Schema-Spalten sind das Excel-Layout, das diese Konfiguration erzeugt."),

            ["st.Ready"] = new Entry("Ready", "Bereit"),
            ["st.LookupResolved"] = new Entry("Resolved", "Aufgelöst"),
            ["st.Committed"] = new Entry("Written", "Geschrieben"),
            ["st.Warning"] = new Entry("Notice", "Hinweis"),
            ["st.LookupAmbiguous"] = new Entry("Ambiguous", "Mehrdeutig"),
            ["st.LookupNotFound"] = new Entry("Not found", "Nicht gefunden"),
            ["st.LookupWrongTargetType"] = new Entry("Wrong target type", "Falscher Zieltyp"),
            ["st.MissingRequiredValue"] = new Entry("Required value missing", "Pflichtfeld fehlt"),
            ["st.InvalidFormat"] = new Entry("Invalid format", "Ungültiges Format"),
            ["st.PermissionIssue"] = new Entry("No permission", "Keine Berechtigung"),
            ["st.WriteBlocked"] = new Entry("Write blocked", "Schreiben blockiert"),
            ["st.CommitFailed"] = new Entry("Write failed", "Schreiben fehlgeschlagen"),
            ["st.DuplicateInFile"] = new Entry("Duplicate in file", "Dublette in Datei"),
            ["st.Skipped"] = new Entry("Skipped", "Übersprungen"),

            ["js.draft"] = new Entry("Draft", "Entwurf"),
            ["js.validated"] = new Entry("Validated", "Validiert"),
            ["js.awaitingConflicts"] = new Entry("Awaiting decision", "Wartet auf Entscheidung"),
            ["js.committing"] = new Entry("Writing…", "Schreibt…"),
            ["js.completed"] = new Entry("Completed", "Abgeschlossen"),
            ["js.completedWithErrors"] = new Entry("With errors", "Mit Fehlern"),
            ["js.aborted"] = new Entry("Aborted", "Abgebrochen"),

            ["val.title"] = new Entry("Configuration check", "Konfigurationsprüfung"),
            ["val.checking"] = new Entry("Checking the configuration against current Dataverse metadata…", "Konfiguration wird gegen die aktuellen Dataverse-Metadaten geprüft…"),
            ["val.ok"] = new Entry("Configuration matches the current schema.", "Konfiguration passt zum aktuellen Schema."),
            ["val.blocked"] = new Entry("Resolve the errors below before importing.", "Bitte die Fehler unten beheben, bevor importiert wird."),
            ["val.recheck"] = new Entry("Re-check", "Erneut prüfen"),
            ["val.entityMissing"] = new Entry("Entity '{target}' not found or not accessible.", "Entität '{target}' nicht gefunden oder nicht zugänglich."),
            ["val.entitySetChanged"] = new Entry("Entity set changed: expected '{expected}', now '{actual}'.", "Entity Set geändert: erwartet '{expected}', jetzt '{actual}'."),
            ["val.primaryIdChanged"] = new Entry("Primary id changed: '{expected}' -> '{actual}'.", "Primär-ID geändert: '{expected}' -> '{actual}'."),
            ["val.attributeMissing"] = new Entry("Column '{target}' no longer exists.", "Spalte '{target}' existiert nicht mehr."),
            ["val.attributeNotWritable"] = new Entry("Column '{target}' is no longer writable.", "Spalte '{target}' ist nicht mehr beschreibbar."),
            ["val.attributeTypeChanged"] = new Entry("Column '{target}' type changed: {was} -> {now}.", "Typ von '{target}' geändert: {was} -> {now}."),
            ["val.lookupAttributeMissing"] = new Entry("Lookup field '{target}' no longer exists.", "Lookup-Feld '{target}' existiert nicht mehr."),
            ["val.lookupAttributeNotLookup"] = new Entry("'{target}' is no longer a lookup (now {now}).", "'{target}' ist kein Lookup mehr (jetzt {now})."),
            ["val.lookupTargetNotAllowed"] = new Entry("Lookup '{target}': target '{entity}' is no longer allowed.", "Lookup '{target}': Ziel '{entity}' ist nicht mehr zulässig."),
            ["val.navPropMissing"] = new Entry("Lookup '{target}': no navigation property for '{entity}'.", "Lookup '{target}': keine Navigation-Property für '{entity}'."),
            ["val.searchAttributeMissing"] = new Entry("Lookup '{target}': search field '{attr}' missing on '{entity}'.", "Lookup '{target}': Suchfeld '{attr}' fehlt auf '{entity}'."),
            ["val.businessKeyAttributeMissing"] = new Entry("Lookup '{target}': business key '{attr}' missing on '{entity}'.", "Lookup '{target}': Business Key '{attr}' fehlt auf '{entity}'."),
            ["val.conditionAttributeMissing"] = new Entry("Lookup '{target}': condition field '{attr}' missing on '{entity}'.", "Lookup '{target}': Bedingungsfeld '{attr}' fehlt auf '{entity}'."),
            ["val.schemaChangedSinceSave"] = new Entry("Schema of '{target}' changed since this configuration was saved - review before importing.", "Schema von '{target}' hat sich seit dem Speichern geändert - bitte vor dem Import prüfen."),

            ["notif.doneTitle"] = new Entry("Import finished", "Import abgeschlossen"),
            ["notif.doneBody"] = new Entry("{written} written · {errors} errors · {conflicts} conflicts", "{written} geschrieben · {errors} Fehler · {conflicts} Konflikte"),
            ["app.loading"] = new Entry("Loading LookupImportPlus…", "LookupImportPlus wird geladen…"),
        };
    }
}
