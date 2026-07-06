using System;
using System.Collections.Generic;
using System.Linq;
using LookupImportPlus.Domain;
using LookupImportPlus.Services.Excel;

namespace LookupImportPlus.Services
{
    /// <summary>
    /// Preflight check of a saved configuration against the CURRENT Dataverse
    /// metadata, so schema drift surfaces as clear issues before a run instead of
    /// a raw exception mid-import. Port of ConfigValidationService.ts.
    /// </summary>
    public sealed class ConfigValidationService
    {
        private readonly MetadataService _metadata;

        public ConfigValidationService(MetadataService metadata)
        {
            _metadata = metadata;
        }

        /// <summary>Stable fingerprint of the metadata a config depends on.</summary>
        public static string FingerprintEntity(EntityMetadata entity)
        {
            var attrs = (entity.Attributes ?? new List<AttributeMetadata>())
                .Select(a =>
                {
                    var targets = a.Lookup?.Targets
                        .Select(t => $"{t.LogicalName}:{t.NavigationProperty}")
                        .OrderBy(s => s, StringComparer.Ordinal)
                        .ToList();
                    var targetPart = targets != null && targets.Count > 0 ? ":" + string.Join("+", targets) : "";
                    return $"{a.LogicalName}:{a.Kind}:{(a.IsWritable ? 1 : 0)}{targetPart}";
                })
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();

            return ManifestHash.Djb2($"{entity.LogicalName}|{entity.EntitySetName}|{entity.PrimaryIdAttribute}|{string.Join("|", attrs)}");
        }

        public ConfigValidationResult Validate(JobConfiguration config)
        {
            var result = new ConfigValidationResult();

            EntityMetadata entity;
            try { entity = _metadata.GetEntity(config.TargetEntity); }
            catch
            {
                result.Issues.Add(new ConfigIssue { Severity = IssueSeverity.Error, Code = ConfigIssueCode.EntityMissing, Target = config.TargetEntity });
                result.Fingerprint = "";
                return result;
            }

            var fingerprint = FingerprintEntity(entity);
            result.Fingerprint = fingerprint;
            var byName = (entity.Attributes ?? new List<AttributeMetadata>())
                .ToDictionary(a => a.LogicalName, a => a, StringComparer.OrdinalIgnoreCase);

            // ── entity-level drift ──
            if (entity.EntitySetName != config.EntitySetName)
                result.Issues.Add(Issue(IssueSeverity.Warning, ConfigIssueCode.EntitySetChanged, config.TargetEntity,
                    ("expected", config.EntitySetName), ("actual", entity.EntitySetName)));
            if (entity.PrimaryIdAttribute != config.PrimaryIdAttribute)
                result.Issues.Add(Issue(IssueSeverity.Warning, ConfigIssueCode.PrimaryIdChanged, config.TargetEntity,
                    ("expected", config.PrimaryIdAttribute), ("actual", entity.PrimaryIdAttribute)));

            // ── columns ──
            foreach (var col in config.Columns)
            {
                if (!byName.TryGetValue(col.Attribute, out var attr))
                {
                    result.Issues.Add(Issue(IssueSeverity.Error, ConfigIssueCode.AttributeMissing, col.Attribute));
                    continue;
                }
                if ((col.Usage == ColumnUsage.ImportExport || col.Usage == ColumnUsage.ImportOnly)
                    && attr.Kind != AttributeKind.Lookup && !attr.IsWritable)
                    result.Issues.Add(Issue(IssueSeverity.Warning, ConfigIssueCode.AttributeNotWritable, col.Attribute));
                if (attr.Kind != col.Kind)
                    result.Issues.Add(Issue(IssueSeverity.Warning, ConfigIssueCode.AttributeTypeChanged, col.Attribute,
                        ("was", col.Kind.ToString()), ("now", attr.Kind.ToString())));
            }

            // ── lookups ──
            foreach (var lk in config.Lookups)
            {
                if (!byName.TryGetValue(lk.LookupAttribute, out var la))
                {
                    result.Issues.Add(Issue(IssueSeverity.Error, ConfigIssueCode.LookupAttributeMissing, lk.LookupAttribute));
                    continue;
                }
                if (la.Kind != AttributeKind.Lookup)
                {
                    result.Issues.Add(Issue(IssueSeverity.Error, ConfigIssueCode.LookupAttributeNotLookup, lk.LookupAttribute,
                        ("now", la.Kind.ToString())));
                    continue;
                }
                var allowed = (la.Lookup?.Targets ?? new List<LookupTarget>())
                    .ToDictionary(tg => tg.LogicalName, tg => tg, StringComparer.OrdinalIgnoreCase);
                foreach (var target in lk.TargetEntities)
                {
                    if (!allowed.TryGetValue(target, out var tgt))
                    {
                        result.Issues.Add(Issue(IssueSeverity.Error, ConfigIssueCode.LookupTargetNotAllowed, lk.LookupAttribute, ("entity", target)));
                        continue;
                    }
                    if (string.IsNullOrEmpty(tgt.NavigationProperty))
                        result.Issues.Add(Issue(IssueSeverity.Error, ConfigIssueCode.NavPropMissing, lk.LookupAttribute, ("entity", target)));
                    CheckTargetAttributes(lk, target, result.Issues);
                }
            }

            // ── metadata drift since save ──
            if (!string.IsNullOrEmpty(config.MetadataFingerprint) && config.MetadataFingerprint != fingerprint)
                result.Issues.Add(Issue(IssueSeverity.Info, ConfigIssueCode.SchemaChangedSinceSave, config.TargetEntity));

            return result;
        }

        private void CheckTargetAttributes(LookupConfig lk, string targetLogical, List<ConfigIssue> issues)
        {
            EntityMetadata target;
            try { target = _metadata.GetEntity(targetLogical); }
            catch { return; }

            var names = new HashSet<string>((target.Attributes ?? new List<AttributeMetadata>()).Select(a => a.LogicalName), StringComparer.OrdinalIgnoreCase);

            LookupTargetOverride over = null;
            if (lk.TargetOverrides != null) lk.TargetOverrides.TryGetValue(targetLogical, out over);

            var searchAttr = !string.IsNullOrEmpty(over?.SearchAttribute) ? over.SearchAttribute : lk.SearchAttribute;
            var bkAttr = !string.IsNullOrEmpty(over?.BusinessKeyAttribute) ? over.BusinessKeyAttribute : lk.BusinessKeyAttribute;

            if (!string.IsNullOrEmpty(searchAttr) && !names.Contains(searchAttr))
                issues.Add(Issue(IssueSeverity.Error, ConfigIssueCode.SearchAttributeMissing, lk.LookupAttribute, ("attr", searchAttr), ("entity", targetLogical)));
            if (!string.IsNullOrEmpty(bkAttr) && !names.Contains(bkAttr))
                issues.Add(Issue(IssueSeverity.Warning, ConfigIssueCode.BusinessKeyAttributeMissing, lk.LookupAttribute, ("attr", bkAttr), ("entity", targetLogical)));

            var conds = over?.Conditions ?? lk.Conditions;
            foreach (var c in conds?.Conditions ?? Enumerable.Empty<Condition>())
            {
                if (!string.IsNullOrEmpty(c.Attribute) && !names.Contains(c.Attribute))
                    issues.Add(Issue(IssueSeverity.Error, ConfigIssueCode.ConditionAttributeMissing, lk.LookupAttribute, ("attr", c.Attribute), ("entity", targetLogical)));
            }
        }

        private static ConfigIssue Issue(IssueSeverity severity, ConfigIssueCode code, string target, params (string, string)[] p)
        {
            var issue = new ConfigIssue { Severity = severity, Code = code, Target = target };
            if (p.Length > 0)
            {
                issue.Params = new Dictionary<string, string>();
                foreach (var (k, v) in p) issue.Params[k] = v;
            }
            return issue;
        }
    }
}
