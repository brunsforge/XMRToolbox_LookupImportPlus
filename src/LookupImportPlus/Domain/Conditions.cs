using System;
using System.Collections.Generic;

namespace LookupImportPlus.Domain
{
    /// <summary>
    /// Structured condition model for lookup resolution filters (conditions.ts).
    /// Conditions are never stored as raw filter strings; they are compiled to a
    /// QueryExpression at query time by the resolver. This keeps them validated,
    /// editable in a builder UI, and safe to serialize into a config snapshot.
    /// </summary>
    public sealed class ConditionValue
    {
        public ValueSourceKind Kind { get; set; }

        /// <summary>For <see cref="ValueSourceKind.Literal"/>: the fixed value.</summary>
        public object Value { get; set; }

        /// <summary>For <see cref="ValueSourceKind.ExcelColumn"/>: the column header.</summary>
        public string Column { get; set; }

        /// <summary>
        /// For <see cref="ValueSourceKind.RelativeDate"/>: offset relative to
        /// today (UTC). Negative = past. Unit is days.
        /// </summary>
        public int OffsetDays { get; set; }

        /// <summary>For <see cref="ValueSourceKind.ContextValue"/>: the key.</summary>
        public string ContextKey { get; set; }

        public static ConditionValue Literal(object value) =>
            new ConditionValue { Kind = ValueSourceKind.Literal, Value = value };

        public static ConditionValue Excel(string column) =>
            new ConditionValue { Kind = ValueSourceKind.ExcelColumn, Column = column };

        public static ConditionValue Relative(int offsetDays) =>
            new ConditionValue { Kind = ValueSourceKind.RelativeDate, OffsetDays = offsetDays };
    }

    public sealed class Condition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>Logical name of the target attribute being filtered.</summary>
        public string Attribute { get; set; }

        public ConditionOperator Operator { get; set; } = ConditionOperator.Eq;

        /// <summary>Absent for <c>Null</c>/<c>NotNull</c> operators.</summary>
        public ConditionValue Value { get; set; }
    }

    public sealed class ConditionGroup
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public GroupLogic Logic { get; set; } = GroupLogic.And;
        public List<Condition> Conditions { get; set; } = new List<Condition>();

        /// <summary>Nested groups — stored, but flattened to AND in the MVP.</summary>
        public List<ConditionGroup> Groups { get; set; } = new List<ConditionGroup>();

        public static ConditionGroup Empty() => new ConditionGroup();
    }
}
