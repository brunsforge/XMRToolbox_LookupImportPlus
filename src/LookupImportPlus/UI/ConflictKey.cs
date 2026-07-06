namespace LookupImportPlus.UI
{
    /// <summary>
    /// Identifies a conflict group (one target field + one source value). Passed
    /// from the conflict basket to the resolve screen. One decision applies to
    /// every row sharing this key.
    /// </summary>
    public sealed class ConflictKey
    {
        public string LookupAttribute { get; set; }
        public string SourceValue { get; set; }
    }
}
