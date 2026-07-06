using System.Collections.Generic;

namespace LookupImportPlus.Domain
{
    /// <summary>A single allowed target of a lookup attribute (metadata.ts).</summary>
    public sealed class LookupTarget
    {
        /// <summary>Logical name of the target entity, e.g. <c>account</c>.</summary>
        public string LogicalName { get; set; }

        /// <summary>EntitySet name of the target, e.g. <c>accounts</c>.</summary>
        public string EntitySetName { get; set; }

        public string DisplayName { get; set; }
        public string PrimaryIdAttribute { get; set; }
        public string PrimaryNameAttribute { get; set; }

        /// <summary>
        /// Navigation property used to bind this lookup to this specific target.
        /// For polymorphic lookups it is target-specific and must come from
        /// relationship metadata, never be guessed.
        /// </summary>
        public string NavigationProperty { get; set; }
    }

    public sealed class OptionMetadata
    {
        public int Value { get; set; }
        public string Label { get; set; }
    }

    public sealed class LookupMetadata
    {
        public LookupKind Kind { get; set; }
        public List<LookupTarget> Targets { get; set; } = new List<LookupTarget>();
    }

    public sealed class AttributeMetadata
    {
        public string LogicalName { get; set; }
        public string DisplayName { get; set; }
        public AttributeKind Kind { get; set; }

        /// <summary>Raw Dataverse attribute type string, kept for diagnostics.</summary>
        public string AttributeType { get; set; }

        public bool IsWritable { get; set; }
        public bool IsRequired { get; set; }
        public bool IsPrimaryId { get; set; }
        public bool IsPrimaryName { get; set; }

        /// <summary>Present only for <see cref="AttributeKind.Lookup"/> attributes.</summary>
        public LookupMetadata Lookup { get; set; }

        /// <summary>Present for Choice/MultiChoice/State/Status attributes.</summary>
        public List<OptionMetadata> Options { get; set; }

        public int? MaxLength { get; set; }
    }

    public sealed class EntityMetadata
    {
        public string LogicalName { get; set; }
        public string DisplayName { get; set; }
        public string DisplayCollectionName { get; set; }

        /// <summary>EntitySet name, e.g. <c>contacts</c>.</summary>
        public string EntitySetName { get; set; }

        public string PrimaryIdAttribute { get; set; }
        public string PrimaryNameAttribute { get; set; }
        public bool IsActivity { get; set; }
        public List<AttributeMetadata> Attributes { get; set; }
    }

    /// <summary>Lightweight entity descriptor for pickers (no attributes loaded).</summary>
    public sealed class EntitySummary
    {
        public string LogicalName { get; set; }
        public string DisplayName { get; set; }
        public string DisplayCollectionName { get; set; }
        public string EntitySetName { get; set; }
        public string PrimaryIdAttribute { get; set; }
        public string PrimaryNameAttribute { get; set; }
    }
}
