namespace LookupImportPlus.Domain
{
    // Enums are serialized as their C# member names (StringEnumConverter). The
    // string values below mirror src/domain/* where practical; since the plugin
    // reads back only its own persisted data, exact casing parity with the TS
    // app is not required.

    /// <summary>What the import does with each incoming row (config.ts OperationType).</summary>
    public enum OperationType
    {
        Create,
        Update,
        CreateOrUpdate,
        UpsertAlternateKey
    }

    /// <summary>How a single Excel column maps to a Dataverse attribute (config.ts).</summary>
    public enum ColumnUsage
    {
        ImportExport,
        ExportOnly,
        ImportOnly,
        Technical
    }

    /// <summary>How multiple candidates for a lookup are handled (config.ts).</summary>
    public enum ConflictStrategy
    {
        Escalate,
        SkipRow,
        FailRow
    }

    /// <summary>Where a condition's right-hand value comes from (conditions.ts).</summary>
    public enum ValueSourceKind
    {
        Literal,
        ExcelColumn,
        RelativeDate,
        CurrentUser,
        ContextValue
    }

    /// <summary>Condition comparison operators (conditions.ts).</summary>
    public enum ConditionOperator
    {
        Eq,
        Ne,
        Gt,
        Ge,
        Lt,
        Le,
        Contains,
        StartsWith,
        Null,
        NotNull,
        In
    }

    public enum GroupLogic
    {
        And,
        Or
    }

    /// <summary>Where export data comes from (config.ts).</summary>
    public enum ExportSourceKind
    {
        Entity,
        SavedView,
        FetchXml
    }

    /// <summary>Attribute categories distinguished for import/export handling (metadata.ts).</summary>
    public enum AttributeKind
    {
        String,
        Memo,
        Integer,
        BigInt,
        Decimal,
        Double,
        Money,
        Boolean,
        DateTime,
        Choice,
        MultiChoice,
        Lookup,
        UniqueIdentifier,
        State,
        Status,
        Unknown
    }

    public enum LookupKind
    {
        Simple,
        Polymorphic
    }

    /// <summary>Per-row evaluation status (import.ts RowStatus).</summary>
    public enum RowStatus
    {
        Ready,
        Warning,
        MissingRequiredValue,
        InvalidFormat,
        LookupResolved,
        LookupNotFound,
        LookupAmbiguous,
        LookupWrongTargetType,
        PermissionIssue,
        DuplicateInFile,
        WriteBlocked,
        Skipped,
        Committed,
        CommitFailed
    }

    /// <summary>Outcome of resolving one lookup value on one row (import.ts).</summary>
    public enum LookupResolutionStatus
    {
        Resolved,
        NotFound,
        Ambiguous,
        WrongTargetType,
        Pending,

        /// <summary>
        /// No lookup value was provided at all (no GUID, business key or search
        /// value). Non-blocking: the lookup is simply left unset. Distinct from
        /// <see cref="NotFound"/>, which means a value was given but not matched.
        /// </summary>
        Empty
    }

    /// <summary>How a match was obtained, for audit/UI (import.ts).</summary>
    public enum ResolutionMethod
    {
        Guid,
        BusinessKey,
        SearchMatch,
        Manual
    }

    public enum ImportMode
    {
        Strict,
        Partial
    }

    public enum ImportJobStatus
    {
        Draft,
        Validated,
        AwaitingConflicts,
        Committing,
        Completed,
        CompletedWithErrors,
        Aborted
    }

    public enum IssueSeverity
    {
        Error,
        Warning,
        Info
    }

    /// <summary>Machine codes for config validation issues (issues.ts).</summary>
    public enum ConfigIssueCode
    {
        EntityMissing,
        EntitySetChanged,
        PrimaryIdChanged,
        AttributeMissing,
        AttributeNotWritable,
        AttributeTypeChanged,
        LookupAttributeMissing,
        LookupAttributeNotLookup,
        LookupTargetNotAllowed,
        NavPropMissing,
        SearchAttributeMissing,
        BusinessKeyAttributeMissing,
        ConditionAttributeMissing,
        SchemaChangedSinceSave
    }

    /// <summary>Role of a generated Excel column (template.ts).</summary>
    public enum TemplateColumnRole
    {
        Value,
        LookupVisible,
        LookupId,
        LookupLogicalName,
        LookupBusinessKey,
        RecordId
    }
}
