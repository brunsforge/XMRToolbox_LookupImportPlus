using System;
using System.Collections.Generic;
using System.IO;
using LookupImportPlus.Domain;

namespace LookupImportPlus.Services
{
    /// <summary>
    /// Persistence for configurations and run history. The Code App used
    /// localStorage; the plugin equivalent is per-plugin JSON files under the
    /// XrmToolBox settings folder (PersistedStore.ts seam). A future
    /// Dataverse-table store can replace this without touching the UI.
    /// </summary>
    public sealed class PersistedStore
    {
        private readonly string _configsPath;
        private readonly string _historyPath;

        public PersistedStore(string baseDirectory)
        {
            var dir = Path.Combine(baseDirectory ?? Path.GetTempPath(), "LookupImportPlus");
            Directory.CreateDirectory(dir);
            _configsPath = Path.Combine(dir, "configs.json");
            _historyPath = Path.Combine(dir, "history.json");
        }

        public List<JobConfiguration> LoadConfigs() => Read<JobConfiguration>(_configsPath);
        public void SaveConfigs(List<JobConfiguration> configs) => Write(_configsPath, configs);

        public List<ImportJob> LoadHistory() => Read<ImportJob>(_historyPath);
        public void SaveHistory(List<ImportJob> jobs) => Write(_historyPath, jobs);

        private static List<T> Read<T>(string path)
        {
            try
            {
                if (!File.Exists(path)) return new List<T>();
                var json = File.ReadAllText(path);
                return Json.Deserialize<List<T>>(json) ?? new List<T>();
            }
            catch
            {
                return new List<T>();
            }
        }

        private static void Write(string path, object value)
        {
            try
            {
                File.WriteAllText(path, Json.Serialize(value));
            }
            catch
            {
                // Disk full / locked — degrade silently (session-only).
            }
        }
    }
}
