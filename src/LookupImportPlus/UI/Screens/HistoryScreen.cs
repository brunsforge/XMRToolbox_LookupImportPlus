namespace LookupImportPlus.UI.Screens
{
    /// <summary>
    /// 3.8 Importhistorie. Tabelle aller Laeufe mit eingefrorenem Konfig-Snapshot
    /// und Zaehlern (Gestartet, Konfiguration+Version, Modus, Zeilen, Geschrieben,
    /// Konflikte, Status). Datenquelle = SettingsManager-Persistenz.
    /// </summary>
    public sealed class HistoryScreen : ScreenControlBase
    {
        public HistoryScreen()
        {
            Controls.Add(PlaceholderView.Build(
                "Importhistorie",
                "DataGridView aus persistierter Historie (SettingsManager). Jede Zeile traegt ihren " +
                "Config-Snapshot fuer Nachvollziehbarkeit bis zur Lookup-Entscheidung. Status-Chip farbig."));
        }
    }
}
