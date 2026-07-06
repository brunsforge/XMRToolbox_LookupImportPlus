using System.Collections.Generic;
using System.Linq;
using LookupImportPlus.Domain;

namespace LookupImportPlus.Services
{
    /// <summary>Helpers for building/editing configurations from live metadata (configBuilder.ts).</summary>
    public static class ConfigBuilder
    {
        public static JobConfiguration BlankConfig()
        {
            return new JobConfiguration
            {
                Operation = OperationType.CreateOrUpdate,
                DefaultMode = ImportMode.Strict,
                IsActive = false
            };
        }

        /// <summary>Technical lookup column names derived from a visible header (English pattern).</summary>
        public static (string GuidColumn, string LogicalNameColumn, string BusinessKeyColumn) DeriveLookupColumns(string visible)
        {
            return ($"{visible} Id", $"{visible} Type", $"{visible} Number");
        }

        public static ColumnConfig ColumnFromAttribute(AttributeMetadata attr, int order)
        {
            return new ColumnConfig
            {
                Attribute = attr.LogicalName,
                Header = string.IsNullOrEmpty(attr.DisplayName) ? attr.LogicalName : attr.DisplayName,
                Usage = ColumnUsage.ImportExport,
                Kind = attr.Kind,
                Order = order
            };
        }

        /// <summary>A sensible default lookup config for a lookup attribute, from its metadata.</summary>
        public static LookupConfig LookupFromAttribute(AttributeMetadata attr)
        {
            var visible = string.IsNullOrEmpty(attr.DisplayName) ? attr.LogicalName : attr.DisplayName;
            var targets = attr.Lookup?.Targets ?? new List<LookupTarget>();
            var first = targets.FirstOrDefault();
            var tech = DeriveLookupColumns(visible);

            Dictionary<string, LookupTargetOverride> overrides = null;
            if (targets.Count > 1)
            {
                overrides = targets.ToDictionary(
                    tg => tg.LogicalName,
                    tg => new LookupTargetOverride
                    {
                        SearchAttribute = string.IsNullOrEmpty(tg.PrimaryNameAttribute) ? "name" : tg.PrimaryNameAttribute
                    });
            }

            var searchAttr = string.IsNullOrEmpty(first?.PrimaryNameAttribute) ? "name" : first.PrimaryNameAttribute;

            return new LookupConfig
            {
                LookupAttribute = attr.LogicalName,
                TargetEntities = targets.Select(tg => tg.LogicalName).ToList(),
                VisibleColumn = visible,
                GuidColumn = tech.GuidColumn,
                LogicalNameColumn = targets.Count > 1 ? tech.LogicalNameColumn : null,
                BusinessKeyColumn = null,
                SearchAttribute = searchAttr,
                BusinessKeyAttribute = null,
                TargetOverrides = overrides,
                Strategy = new ResolutionStrategy { UseGuidColumn = true, UseBusinessKey = false, UseSearchMatch = true },
                Conditions = ConditionGroup.Empty(),
                ConflictStrategy = ConflictStrategy.Escalate,
                CandidateDisplayAttributes = first != null && !string.IsNullOrEmpty(first.PrimaryNameAttribute)
                    ? new List<string> { first.PrimaryNameAttribute }
                    : new List<string>()
            };
        }
    }
}
