using System;
using System.Collections.Generic;

namespace LookupImportPlus.Domain
{
    /// <summary>One selected import column and its intended usage (Tab 3).</summary>
    public sealed class ColumnConfig
    {
        /// <summary>Attribute logical name.</summary>
        public string LogicalName { get; set; }

        /// <summary>Column header used in the Excel file (defaults to display name).</summary>
        public string ExcelHeader { get; set; }
    }

    /// <summary>
    /// A versioned import job configuration. Port of src/domain/config.ts and the
    /// central object of the app: configuration comes first, every run stores an
    /// immutable snapshot of the config it used.
    /// </summary>
    public sealed class JobConfiguration
    {
        /// <summary>Stable id (localStorage/SettingsManager key equivalent).</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>Target table logical name.</summary>
        public string TargetEntity { get; set; }

        /// <summary>Whether columns come from the entity or a saved view.</summary>
        public SourceKind Source { get; set; } = SourceKind.Entity;

        /// <summary>Saved query id when <see cref="Source"/> is SavedView.</summary>
        public Guid? SavedQueryId { get; set; }

        public ImportOperation Operation { get; set; } = ImportOperation.Create;
        public WriteMode WriteMode { get; set; } = WriteMode.Strict;

        public List<ColumnConfig> Columns { get; set; } = new List<ColumnConfig>();
        public List<LookupConfig> Lookups { get; set; } = new List<LookupConfig>();

        /// <summary>Monotonically increasing version; bumped on each save.</summary>
        public int Version { get; set; } = 1;

        /// <summary>True until the config has been completed/saved as final.</summary>
        public bool IsDraft { get; set; } = true;
    }
}
