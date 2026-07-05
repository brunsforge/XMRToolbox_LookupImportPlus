namespace LookupImportPlus.Domain
{
    /// <summary>How a target row is written. Port of the Code App's operation type.</summary>
    public enum ImportOperation
    {
        Create,
        Update,
        CreateOrUpdate
    }

    /// <summary>
    /// Default write mode of a job (src/domain/config.ts).
    /// strict = nothing is written until every row resolves;
    /// partial = clean rows are committed immediately.
    /// </summary>
    public enum WriteMode
    {
        Strict,
        Partial
    }

    /// <summary>
    /// What to do when a lookup resolves to more than one candidate
    /// (src/domain/config.ts). The row is never silently guessed.
    /// </summary>
    public enum ConflictStrategy
    {
        Escalate,
        Skip,
        Fail
    }

    /// <summary>
    /// Source of a condition's right-hand value (src/domain/conditions.ts).
    /// </summary>
    public enum ConditionValueType
    {
        /// <summary>A constant literal.</summary>
        FixedValue,

        /// <summary>Another Excel column of the same row.</summary>
        ExcelColumn,

        /// <summary>Relative date in days, e.g. @utcToday(-30d), resolved at run time.</summary>
        RelativeDate
    }

    /// <summary>Where a config sources its importable columns (Tab 1).</summary>
    public enum SourceKind
    {
        /// <summary>The entity itself.</summary>
        Entity,

        /// <summary>A saved query (savedquery); its columns become importable.</summary>
        SavedView
    }
}
