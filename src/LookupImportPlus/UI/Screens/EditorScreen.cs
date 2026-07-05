namespace LookupImportPlus.UI.Screens
{
    /// <summary>
    /// 3.2 Konfigurations-Editor (Assistent). TabControl mit vier Tabs;
    /// Tabs 2-4 gesperrt, bis eine Zielentitaet gewaehlt ist.
    ///   1 - Entitaet und Quelle, 2 - Allgemein, 3 - Spalten (n), 4 - Lookups (n).
    /// Kopf-Buttons: Abbrechen, Speichern, Import starten.
    /// </summary>
    public sealed class EditorScreen : ScreenControlBase
    {
        public EditorScreen()
        {
            Controls.Add(PlaceholderView.Build(
                "Konfigurations-Editor",
                "TabControl: [1 Entitaet & Quelle] [2 Allgemein] [3 Spalten (n)] [4 Lookups (n)]. " +
                "Tabs 2-4 Enabled=false bis Entitaet gewaehlt. Tab 4 (Lookups) ist das Kernstueck: " +
                "pro Lookup-Spalte eine Karte mit GUID-/Business-Key-/Suchfeld-Aufloesung."));
        }
    }
}
