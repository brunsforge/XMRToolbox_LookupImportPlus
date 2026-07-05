using System.Collections.Generic;

namespace LookupImportPlus.Domain
{
    /// <summary>
    /// Per-target resolution settings for one target table of a (possibly
    /// polymorphic) lookup. Port of the targetOverrides entry in config.ts.
    /// </summary>
    public sealed class LookupTargetOverride
    {
        /// <summary>Target logical name, e.g. "account" or "contact".</summary>
        public string TargetEntity { get; set; }

        /// <summary>String attribute searched by display name, e.g. account.name.</summary>
        public string SearchAttribute { get; set; }

        /// <summary>Optional alternate-key attribute for business-key matching.</summary>
        public string BusinessKeyAttribute { get; set; }

        /// <summary>Additional narrowing conditions (stage 3).</summary>
        public List<Condition> Conditions { get; set; } = new List<Condition>();
    }

    /// <summary>
    /// Resolution rules for one lookup attribute. Port of config.ts. The fixed
    /// matching order is: 1) GUID column, 2) business key, 3) search field +
    /// conditions. Never guessed.
    /// </summary>
    public sealed class LookupConfig
    {
        /// <summary>Lookup attribute logical name, e.g. "parentcustomerid".</summary>
        public string LookupAttribute { get; set; }

        /// <summary>Human-readable Excel column, e.g. "Parent Account".</summary>
        public string VisibleColumn { get; set; }

        /// <summary>Excel column holding the record GUID (stage 1), if provided.</summary>
        public string GuidColumn { get; set; }

        /// <summary>Excel column holding a unique business key (stage 2), if provided.</summary>
        public string BusinessKeyColumn { get; set; }

        /// <summary>Target tables actually referenced (polymorphic-aware).</summary>
        public List<string> TargetEntities { get; set; } = new List<string>();

        /// <summary>Per-target search settings, keyed by target logical name.</summary>
        public Dictionary<string, LookupTargetOverride> TargetOverrides { get; set; }
            = new Dictionary<string, LookupTargetOverride>();

        /// <summary>What to do on ambiguity.</summary>
        public ConflictStrategy ConflictStrategy { get; set; } = ConflictStrategy.Escalate;
    }
}
