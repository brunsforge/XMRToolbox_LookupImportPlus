using System;
using System.Collections.Generic;

namespace LookupImportPlus.Domain
{
    /// <summary>How one Excel column maps to a Dataverse attribute (config.ts).</summary>
    public sealed class ColumnConfig
    {
        /// <summary>Attribute logical name on the target entity.</summary>
        public string Attribute { get; set; }

        /// <summary>Header shown in the Excel file. Defaults to the display name.</summary>
        public string Header { get; set; }

        public ColumnUsage Usage { get; set; } = ColumnUsage.ImportExport;

        /// <summary>Attribute kind, cached from metadata for offline validation.</summary>
        public AttributeKind Kind { get; set; }

        /// <summary>Order within the exported sheet.</summary>
        public int Order { get; set; }
    }

    /// <summary>How a lookup value is resolved to a target record (config.ts).</summary>
    public sealed class ResolutionStrategy
    {
        public bool UseGuidColumn { get; set; } = true;
        public bool UseBusinessKey { get; set; }
        public bool UseSearchMatch { get; set; } = true;
    }

    /// <summary>Per-target field overrides for polymorphic lookups (config.ts).</summary>
    public sealed class LookupTargetOverride
    {
        public string SearchAttribute { get; set; }
        public string BusinessKeyAttribute { get; set; }
        public ConditionGroup Conditions { get; set; }
    }

    /// <summary>Configuration for one lookup attribute on the target entity (config.ts).</summary>
    public sealed class LookupConfig
    {
        public string Id { get; set; } = "lk-" + Guid.NewGuid().ToString("N");

        /// <summary>Lookup attribute on the target record, e.g. <c>parentcustomerid</c>.</summary>
        public string LookupAttribute { get; set; }

        /// <summary>Allowed target tables (&gt;1 ⇒ polymorphic). Logical names.</summary>
        public List<string> TargetEntities { get; set; } = new List<string>();

        /// <summary>Human-readable Excel column carrying the lookup value.</summary>
        public string VisibleColumn { get; set; }

        /// <summary>Technical GUID column, e.g. <c>Parent Account Id</c>.</summary>
        public string GuidColumn { get; set; }

        /// <summary>Technical target-table column (required for polymorphic).</summary>
        public string LogicalNameColumn { get; set; }

        /// <summary>Optional business-key column.</summary>
        public string BusinessKeyColumn { get; set; }

        /// <summary>Default attribute searched on the target entity, e.g. <c>name</c>.</summary>
        public string SearchAttribute { get; set; }

        /// <summary>Default attribute compared against the business-key column.</summary>
        public string BusinessKeyAttribute { get; set; }

        /// <summary>Per-target overrides keyed by target logical name (polymorphic).</summary>
        public Dictionary<string, LookupTargetOverride> TargetOverrides { get; set; }

        public ResolutionStrategy Strategy { get; set; } = new ResolutionStrategy();

        /// <summary>Extra filter conditions applied during search matching.</summary>
        public ConditionGroup Conditions { get; set; } = ConditionGroup.Empty();

        public ConflictStrategy ConflictStrategy { get; set; } = ConflictStrategy.Escalate;

        /// <summary>Attributes shown for each candidate in the conflict dialog.</summary>
        public List<string> CandidateDisplayAttributes { get; set; } = new List<string>();
    }

    /// <summary>A single validation rule applied to a column during the dry run.</summary>
    public sealed class ValidationRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Attribute { get; set; }

        /// <summary>"required" | "regex" | "range" | "custom".</summary>
        public string Kind { get; set; } = "required";

        public Dictionary<string, object> Params { get; set; }
        public string Message { get; set; }
    }

    public sealed class ExportSource
    {
        public ExportSourceKind Kind { get; set; } = ExportSourceKind.Entity;

        /// <summary>For SavedView: the savedquery id. For FetchXml: the raw FetchXML.</summary>
        public string Reference { get; set; }
    }

    /// <summary>
    /// The versioned description of how one Excel file is exported from and
    /// imported back into one Dataverse entity (config.ts JobConfiguration).
    /// Every import run captures an immutable snapshot of the config it used.
    /// </summary>
    public sealed class JobConfiguration
    {
        public const int ConfigSchemaVersion = 1;

        public string Id { get; set; } = "cfg-" + Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";

        /// <summary>Target entity logical name, e.g. <c>contact</c>.</summary>
        public string TargetEntity { get; set; } = "";

        /// <summary>Target entity set name, e.g. <c>contacts</c>.</summary>
        public string EntitySetName { get; set; } = "";

        /// <summary>Primary id attribute of the target entity, e.g. <c>contactid</c>.</summary>
        public string PrimaryIdAttribute { get; set; } = "";

        public OperationType Operation { get; set; } = OperationType.CreateOrUpdate;
        public ExportSource ExportSource { get; set; } = new ExportSource();

        public List<ColumnConfig> Columns { get; set; } = new List<ColumnConfig>();
        public List<LookupConfig> Lookups { get; set; } = new List<LookupConfig>();
        public List<ValidationRule> ValidationRules { get; set; } = new List<ValidationRule>();

        /// <summary>Default write behavior for runs created from this config.</summary>
        public ImportMode DefaultMode { get; set; } = ImportMode.Strict;

        /// <summary>
        /// Fingerprint of the target entity's relevant metadata at save time.
        /// Used to detect schema drift since the config was last validated.
        /// </summary>
        public string MetadataFingerprint { get; set; }

        /// <summary>Monotonic config version, bumped on every saved edit.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Shape version of this document, for migrations.</summary>
        public int SchemaVersion { get; set; } = ConfigSchemaVersion;

        public bool IsActive { get; set; }
        public string CreatedOn { get; set; }
        public string ModifiedOn { get; set; }
    }
}
