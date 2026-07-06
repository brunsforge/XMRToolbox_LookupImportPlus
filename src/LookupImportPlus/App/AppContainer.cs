using System;
using System.Collections.Generic;
using System.Linq;
using LookupImportPlus.Data;
using LookupImportPlus.Domain;
using LookupImportPlus.Services;
using LookupImportPlus.Services.Excel;

namespace LookupImportPlus.App
{
    /// <summary>
    /// Composition root (port of src/app/container.ts). Wires the services over
    /// the host connection and owns the in-memory config list, run history, and
    /// the active import job shared across the run/conflict/resolve screens.
    /// </summary>
    public sealed class AppContainer
    {
        public DataverseContext Ctx { get; }
        public MetadataService Metadata { get; }
        public LookupResolver Resolver { get; }
        public ImportRunner Runner { get; }
        public ConfigValidationService Validation { get; }
        public DataExportService Export { get; }
        public ExcelTemplateService Template { get; }
        public ExcelParserService Parser { get; }
        public ViewService Views { get; }
        public PersistedStore Store { get; }

        private readonly List<JobConfiguration> _configs;
        private readonly List<ImportJob> _history;

        /// <summary>The run currently being worked on (upload → conflicts → resolve).</summary>
        public ImportJob ActiveJob { get; set; }

        public AppContainer(DataverseContext ctx, string settingsDirectory)
        {
            Ctx = ctx;
            Metadata = new MetadataService(ctx);
            Resolver = new LookupResolver(ctx, Metadata);
            Runner = new ImportRunner(ctx, Metadata, Resolver);
            Validation = new ConfigValidationService(Metadata);
            Export = new DataExportService(ctx, Metadata);
            Template = new ExcelTemplateService();
            Parser = new ExcelParserService();
            Views = new ViewService(ctx);
            Store = new PersistedStore(settingsDirectory);

            _configs = Store.LoadConfigs();
            _history = Store.LoadHistory();
        }

        // ── configurations ───────────────────────────────────────
        public IReadOnlyList<JobConfiguration> ListConfigs() => _configs;

        public JobConfiguration GetConfig(string id) => _configs.FirstOrDefault(c => c.Id == id);

        public void SaveConfig(JobConfiguration config)
        {
            var now = DateTime.UtcNow.ToString("o");
            var existing = _configs.FindIndex(c => c.Id == config.Id);
            config.IsActive = true;
            config.ModifiedOn = now;
            if (existing >= 0)
            {
                config.Version = _configs[existing].Version + 1;
                if (string.IsNullOrEmpty(config.CreatedOn)) config.CreatedOn = _configs[existing].CreatedOn ?? now;
                _configs[existing] = config;
            }
            else
            {
                if (string.IsNullOrEmpty(config.CreatedOn)) config.CreatedOn = now;
                _configs.Add(config);
            }
            Store.SaveConfigs(_configs);
        }

        public void DeleteConfig(string id)
        {
            _configs.RemoveAll(c => c.Id == id);
            Store.SaveConfigs(_configs);
        }

        // ── history ──────────────────────────────────────────────
        public IReadOnlyList<ImportJob> ListHistory() => _history.OrderByDescending(j => j.StartedOn).ToList();

        public void AddHistory(ImportJob job)
        {
            _history.Add(job);
            Store.SaveHistory(_history);
        }

        // ── localized rendering helpers ──────────────────────────

        /// <summary>Localized message for a validation issue.</summary>
        public static string IssueMessage(ConfigIssue issue)
        {
            var key = "val." + Camel(issue.Code.ToString());
            var parameters = new Dictionary<string, object> { ["target"] = issue.Target };
            if (issue.Params != null)
                foreach (var kv in issue.Params) parameters[kv.Key] = kv.Value;
            return I18n.T(key, parameters);
        }

        /// <summary>Localized label for a row status (status chip).</summary>
        public static string StatusLabel(RowStatus status) => I18n.T("st." + status);

        /// <summary>Localized label for a job status.</summary>
        public static string JobStatusLabel(ImportJobStatus status) => I18n.T("js." + Camel(status.ToString()));

        private static string Camel(string s)
            => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);
    }
}
