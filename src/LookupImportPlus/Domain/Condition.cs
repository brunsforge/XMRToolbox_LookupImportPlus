namespace LookupImportPlus.Domain
{
    /// <summary>
    /// A single search condition applied when resolving a lookup via the
    /// search field (stage 3). Port of src/domain/conditions.ts. Compiled to a
    /// QueryExpression ConditionExpression at run time; relative dates are
    /// resolved then (see conditionCompiler.ts).
    /// </summary>
    public sealed class Condition
    {
        /// <summary>Logical name of the target attribute on the left-hand side.</summary>
        public string TargetField { get; set; }

        /// <summary>Operator, e.g. eq / ne / ge / le. Maps to ConditionOperator.</summary>
        public string Operator { get; set; }

        /// <summary>Where the right-hand value comes from.</summary>
        public ConditionValueType ValueType { get; set; }

        /// <summary>
        /// The value: a literal (FixedValue), an Excel column name (ExcelColumn),
        /// or a signed day offset as string, e.g. "-30" (RelativeDate).
        /// </summary>
        public string Value { get; set; }
    }
}
