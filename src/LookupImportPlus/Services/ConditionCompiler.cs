using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LookupImportPlus.Domain;
using Query = Microsoft.Xrm.Sdk.Query;

namespace LookupImportPlus.Services
{
    /// <summary>
    /// Compiles the structured <see cref="ConditionGroup"/> model into Dataverse
    /// <see cref="Query.ConditionExpression"/>s, resolving row- and time-relative
    /// values against a run context. Port of src/services/conditionCompiler.ts —
    /// the only place structured conditions become a query. Also emits a
    /// human-readable filter (for the resolve screen / audit) and records every
    /// resolved relative-date anchor.
    ///
    /// MVP: groups are flattened with AND (the <c>Logic</c> field is honored only
    /// as AND; OR is parsed but AND-joined).
    /// </summary>
    public sealed class CompiledConditions
    {
        public List<Query.ConditionExpression> Conditions { get; } = new List<Query.ConditionExpression>();
        public Dictionary<string, string> TimeAnchors { get; } = new Dictionary<string, string>();
        public List<string> Readable { get; } = new List<string>();

        public string ReadableFilter => string.Join(" and ", Readable);
    }

    public sealed class CompileContext
    {
        /// <summary>Excel row values keyed by header — source for excelColumn values.</summary>
        public IReadOnlyDictionary<string, object> Row { get; set; }

        /// <summary>Run timestamp; relativeDate values are computed from this.</summary>
        public DateTime Now { get; set; } = DateTime.UtcNow;
    }

    public static class ConditionCompiler
    {
        /// <summary>UTC midnight of <paramref name="baseDate"/> shifted by days.</summary>
        public static DateTime ResolveRelativeDate(int offsetDays, DateTime baseDate)
        {
            var midnight = new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, 0, 0, 0, DateTimeKind.Utc);
            return midnight.AddDays(offsetDays);
        }

        public static CompiledConditions Compile(ConditionGroup group, CompileContext ctx)
        {
            var result = new CompiledConditions();
            if (group == null) return result;

            foreach (var c in group.Conditions)
            {
                if (TryCompile(c, ctx, result, out var expr, out var readable))
                {
                    if (expr != null) result.Conditions.Add(expr);
                    if (readable != null) result.Readable.Add(readable);
                }
            }

            foreach (var nested in group.Groups ?? Enumerable.Empty<ConditionGroup>())
            {
                var sub = Compile(nested, ctx);
                result.Conditions.AddRange(sub.Conditions);
                foreach (var kv in sub.TimeAnchors) result.TimeAnchors[kv.Key] = kv.Value;
                result.Readable.AddRange(sub.Readable);
            }

            return result;
        }

        /// <summary>
        /// Compile one condition. Returns false when it can't be represented
        /// (e.g. an excelColumn whose cell is empty — dropped rather than
        /// producing a filter that matches unexpectedly).
        /// </summary>
        private static bool TryCompile(
            Condition cond,
            CompileContext ctx,
            CompiledConditions acc,
            out Query.ConditionExpression expr,
            out string readable)
        {
            expr = null;
            readable = null;
            if (string.IsNullOrEmpty(cond.Attribute)) return false;

            if (cond.Operator == ConditionOperator.Null)
            {
                expr = new Query.ConditionExpression(cond.Attribute, Query.ConditionOperator.Null);
                readable = $"{cond.Attribute} eq null";
                return true;
            }
            if (cond.Operator == ConditionOperator.NotNull)
            {
                expr = new Query.ConditionExpression(cond.Attribute, Query.ConditionOperator.NotNull);
                readable = $"{cond.Attribute} ne null";
                return true;
            }
            if (cond.Operator == ConditionOperator.In)
                throw new NotSupportedException("Operator 'in' is not supported in the MVP compiler yet.");

            if (!TryResolveValue(cond.Value, cond.Attribute, cond.Operator, ctx, acc, out var value, out var token))
                return false;

            switch (cond.Operator)
            {
                case ConditionOperator.Eq:
                    expr = new Query.ConditionExpression(cond.Attribute, Query.ConditionOperator.Equal, value);
                    readable = $"{cond.Attribute} eq {token}";
                    break;
                case ConditionOperator.Ne:
                    expr = new Query.ConditionExpression(cond.Attribute, Query.ConditionOperator.NotEqual, value);
                    readable = $"{cond.Attribute} ne {token}";
                    break;
                case ConditionOperator.Gt:
                    expr = new Query.ConditionExpression(cond.Attribute, Query.ConditionOperator.GreaterThan, value);
                    readable = $"{cond.Attribute} gt {token}";
                    break;
                case ConditionOperator.Ge:
                    expr = new Query.ConditionExpression(cond.Attribute, Query.ConditionOperator.GreaterEqual, value);
                    readable = $"{cond.Attribute} ge {token}";
                    break;
                case ConditionOperator.Lt:
                    expr = new Query.ConditionExpression(cond.Attribute, Query.ConditionOperator.LessThan, value);
                    readable = $"{cond.Attribute} lt {token}";
                    break;
                case ConditionOperator.Le:
                    expr = new Query.ConditionExpression(cond.Attribute, Query.ConditionOperator.LessEqual, value);
                    readable = $"{cond.Attribute} le {token}";
                    break;
                case ConditionOperator.Contains:
                    expr = new Query.ConditionExpression(cond.Attribute, Query.ConditionOperator.Like, $"%{value}%");
                    readable = $"contains({cond.Attribute},{token})";
                    break;
                case ConditionOperator.StartsWith:
                    expr = new Query.ConditionExpression(cond.Attribute, Query.ConditionOperator.Like, $"{value}%");
                    readable = $"startswith({cond.Attribute},{token})";
                    break;
                default:
                    return false;
            }
            return true;
        }

        private static bool TryResolveValue(
            ConditionValue value,
            string attribute,
            ConditionOperator op,
            CompileContext ctx,
            CompiledConditions acc,
            out object resolved,
            out string token)
        {
            resolved = null;
            token = null;
            if (value == null) return false;

            switch (value.Kind)
            {
                case ValueSourceKind.Literal:
                    resolved = value.Value;
                    token = LiteralToken(value.Value);
                    return resolved != null;

                case ValueSourceKind.ExcelColumn:
                {
                    object raw = null;
                    ctx.Row?.TryGetValue(value.Column ?? "", out raw);
                    var s = raw?.ToString();
                    if (string.IsNullOrEmpty(s)) return false;
                    resolved = raw;
                    token = LiteralToken(raw);
                    return true;
                }

                case ValueSourceKind.RelativeDate:
                {
                    var dt = ResolveRelativeDate(value.OffsetDays, ctx.Now);
                    var iso = dt.ToString("o", CultureInfo.InvariantCulture);
                    acc.TimeAnchors[$"{attribute} {OpText(op)} @utcToday({value.OffsetDays}d)"] = iso;
                    resolved = dt;
                    token = iso;
                    return true;
                }

                default:
                    throw new NotSupportedException(
                        $"Value source '{value.Kind}' is not supported in the MVP compiler yet.");
            }
        }

        private static string LiteralToken(object value)
        {
            if (value == null) return "null";
            if (value is bool b) return b ? "true" : "false";
            if (value is int || value is long || value is double || value is decimal)
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            return "'" + value.ToString().Replace("'", "''") + "'";
        }

        private static string OpText(ConditionOperator op)
        {
            switch (op)
            {
                case ConditionOperator.Eq: return "eq";
                case ConditionOperator.Ne: return "ne";
                case ConditionOperator.Gt: return "gt";
                case ConditionOperator.Ge: return "ge";
                case ConditionOperator.Lt: return "lt";
                case ConditionOperator.Le: return "le";
                default: return op.ToString().ToLowerInvariant();
            }
        }
    }
}
