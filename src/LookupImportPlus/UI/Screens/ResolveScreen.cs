namespace LookupImportPlus.UI.Screens
{
    /// <summary>
    /// 3.7 Konflikt aufloesen (Detail). Zeigt die genutzte Abfrage inkl. Zeitanker,
    /// Kandidatenliste (Radio, mit Deep-Link "Oeffnen"), Checkbox "auf alle n Zeilen
    /// anwenden". Auswahl uebernehmen schreibt zurueck UND protokolliert (Audit).
    /// </summary>
    public sealed class ResolveScreen : ScreenControlBase
    {
        public ResolveScreen()
        {
            Controls.Add(PlaceholderView.Build(
                "Konflikt aufloesen",
                "Quellwert + Zielfeld, Warnbox (warum nicht eindeutig), read-only Abfrageanzeige mit " +
                "aufgeloestem Zeitanker, Kandidaten-Radioliste (Name/BusinessKey/modifiedon/GUID/Typ/Oeffnen), " +
                "Checkbox 'auf alle Zeilen anwenden', Buttons Ueberspringen / Auswahl uebernehmen -> / Zur Liste."));
        }
    }
}
