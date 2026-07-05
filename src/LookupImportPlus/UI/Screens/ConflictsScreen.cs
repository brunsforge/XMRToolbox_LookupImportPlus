namespace LookupImportPlus.UI.Screens
{
    /// <summary>
    /// 3.6 Konfliktkorb. Gruppiert nach Quellwert; je Gruppe Zielfeld, betroffene
    /// Zeilen, Kandidatenzahl, Status. Button "Aufloesen" (>=1 Kandidat) bzw.
    /// "Bearbeiten" (0 Treffer). Nichts wird automatisch geraten.
    /// </summary>
    public sealed class ConflictsScreen : ScreenControlBase
    {
        public ConflictsScreen()
        {
            Controls.Add(PlaceholderView.Build(
                "Konfliktkorb",
                "DataGridView gruppiert nach Quellwert: Quellwert, Zielfeld, Betroffen, Kandidaten, Status. " +
                "Button-Spalte Aufloesen -> / Bearbeiten ->. Eine Entscheidung gilt fuer die ganze Gruppe."));
        }
    }
}
