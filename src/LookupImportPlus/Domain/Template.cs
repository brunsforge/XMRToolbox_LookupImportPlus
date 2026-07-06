using System.Collections.Generic;

namespace LookupImportPlus.Domain
{
    /// <summary>
    /// Excel template manifest — the contract embedded in the hidden
    /// <c>_LookupImportPlus</c> worksheet so the importer never relies on visible
    /// headers alone (template.ts).
    /// </summary>
    public static class TemplateConstants
    {
        public const int TemplateSchemaVersion = 1;

        /// <summary>Hidden worksheet name (state "veryHidden").</summary>
        public const string ManifestSheet = "_LookupImportPlus";

        public const string DataSheet = "Daten";

        /// <summary>Technical source-id column that drives update-vs-create on reimport.</summary>
        public const string RecordIdColumn = "lip__recordid";
    }

    public sealed class TemplateColumn
    {
        public string Header { get; set; }

        /// <summary>Target attribute, when the column maps to one.</summary>
        public string Attribute { get; set; }

        public TemplateColumnRole Role { get; set; }

        /// <summary>Hidden by default in the sheet.</summary>
        public bool Technical { get; set; }

        /// <summary>For lookup technical columns: which lookup config they belong to.</summary>
        public string LookupId { get; set; }
    }

    public sealed class TemplateManifest
    {
        public string ConfigId { get; set; }
        public string ConfigName { get; set; }
        public int ConfigVersion { get; set; }
        public int SchemaVersion { get; set; }
        public string TargetEntity { get; set; }
        public string EntitySetName { get; set; }
        public string Operation { get; set; }
        public List<TemplateColumn> Columns { get; set; } = new List<TemplateColumn>();

        /// <summary>Integrity hash of the manifest (excluding this + GeneratedOn).</summary>
        public string Hash { get; set; }
        public string GeneratedOn { get; set; }
    }
}
