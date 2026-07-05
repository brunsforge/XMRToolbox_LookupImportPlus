namespace LookupImportPlus.UI
{
    /// <summary>
    /// The screens of the plugin, 1:1 with the Code App (src/app/AppContext.tsx).
    /// </summary>
    public enum ScreenName
    {
        Configs,    // 3.1 Job-Konfigurationen (start page)
        Editor,     // 3.2 Konfigurations-Editor (wizard)
        ImportRun,  // 3.5 Importlauf
        Conflicts,  // 3.6 Konfliktkorb
        Resolve,    // 3.7 Konflikt aufloesen
        History     // 3.8 Importhistorie
    }
}
