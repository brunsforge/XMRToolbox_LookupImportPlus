using System.Drawing;
using System.Windows.Forms;

namespace LookupImportPlus.UI.Screens
{
    /// <summary>
    /// 3.1 Job-Konfigurationen (Startseite). Liste von Konfig-"Karten" mit
    /// Export / Bearbeiten / Import starten / Loeschen, plus Kopf-Buttons
    /// "Neue Konfiguration" und "Excel importieren".
    /// </summary>
    public sealed class ConfigsScreen : ScreenControlBase
    {
        public ConfigsScreen()
        {
            Controls.Add(PlaceholderView.Build(
                "Job-Konfigurationen",
                "FlowLayoutPanel mit Konfig-Karten. Kopf: [Neue Konfiguration] [Excel importieren]. " +
                "Pro Karte: Zieltabelle, Operation, N Spalten/M Lookups, Version, Entwurfsstatus, " +
                "Buttons Export/Bearbeiten/Import starten/Loeschen."));
        }
    }
}
