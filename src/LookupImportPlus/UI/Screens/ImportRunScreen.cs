namespace LookupImportPlus.UI.Screens
{
    /// <summary>
    /// 3.5 Importlauf. XLSX hochladen -> Konfigurationspruefung (Schema-Drift) ->
    /// Dry Run (jede Zeile klassifiziert) -> Statuskacheln -> Schreibmodus
    /// Strict/Partial -> Commit (ExecuteMultipleRequest).
    /// </summary>
    public sealed class ImportRunScreen : ScreenControlBase
    {
        public ImportRunScreen()
        {
            Controls.Add(PlaceholderView.Build(
                "Importlauf",
                "[XLSX hochladen] -> Schema-Drift-Preflight -> Dry Run (WorkAsync+ReportProgress) -> " +
                "Statuskacheln (Bereit/Konflikte/Fehler/Zeilen) -> Strict/Partial -> [Commit (n)]. " +
                "Commit schreibt via ExecuteMultipleRequest in Batches."));
        }
    }
}
